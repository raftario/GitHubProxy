using System;
using System.IO;
using System.Threading.Tasks;
using GitHubProxy.Config;
using GitHubProxy.IO;
using LibGit2Sharp;
using Nett;
using Octokit;
using GitCredentials = LibGit2Sharp.Credentials;
using GitHubCredentials = Octokit.Credentials;
using GitRepository = LibGit2Sharp.Repository;
using GitHubRepository = Octokit.Repository;
using Signature = LibGit2Sharp.Signature;

namespace GitHubProxy
{
    internal sealed class Program
    {
        private ProxyConfig _config;

        private Signature _defaultSignature;

        private GitHubClient _ghClient;
        private GitHubRepository _ghDest;
        private GitHubRepository _ghSrc;
        private User _ghUser;

        internal static void Main()
        {
            try
            {
                new Program().AsyncMain().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private async Task AsyncMain()
        {
            _config = Toml.ReadFile<ProxyConfig>("config.toml");

            _defaultSignature = new Signature(new Identity(_config.DefaultAuthor.Name, _config.DefaultAuthor.Email),
                DateTimeOffset.Now);

            _ghClient = new GitHubClient(new ProductHeaderValue("GitHubProxy"))
                {Credentials = new GitHubCredentials(_config.Token)};
            _ghUser = await _ghClient.User.Current();

            Logger.Debug(_ghUser.Id);
            Logger.Info($"Logged in to GitHub as {_ghUser.Name} ({_ghUser.Login})");

            _ghSrc = await _ghClient.Repository.Get(_config.Source.User, _config.Source.Repo);
            _ghDest = await _ghClient.Repository.Get(_config.Destination.User, _config.Destination.Repo);

            Logger.Debug(_ghSrc.Id);
            Logger.Debug(_ghDest.Id);
            Logger.Info($"Proxying {_ghSrc.Owner.Login}/{_ghSrc.Name} to {_ghDest.Owner.Login}/{_ghDest.Name}");

            await SetupSrcRepo();
            using (var srcRepo = new GitRepository("srcRepo"))
            {
                Logger.Debug(srcRepo.Info.Path);
                foreach (var branch in srcRepo.Branches)
                {
                    Logger.Debug(branch.ToString());
                }
            }

            while (true)
            {
                Console.WriteLine();
                Logger.Info("Proxying...");

                await ProxyCommits();
                _ghSrc = await _ghClient.Repository.Get(_config.Source.User, _config.Source.Repo);
                _ghDest = await _ghClient.Repository.Get(_config.Destination.User, _config.Destination.Repo);
                await ProxyReleases();

                Logger.Info("Done.");
                await Task.Delay(_config.Interval * 60 * 1000);
            }
        }

        private GitCredentials GitCredentials(string url, string user, SupportedCredentialTypes cred)
        {
            return new UsernamePasswordCredentials {Username = _ghUser.Login, Password = _config.Token};
        }

        private async Task SetupSrcRepo()
        {
            if (!Directory.Exists("srcRepo"))
            {
                Logger.Info("Cloning source repository... ", false);

                Directory.CreateDirectory("srcRepo");
                await Task.Run(() => GitRepository.Clone(_ghSrc.CloneUrl, "srcRepo",
                    new CloneOptions {CredentialsProvider = GitCredentials}));

                Console.WriteLine("Done.");
            }
        }

        private async Task ProxyCommits()
        {
            Logger.Info("Proxying commits...");

            foreach (var branch in _config.Source.Branches)
            {
                await PullSrcRepo(branch);
            }

            await AsyncDirectory.Copy("srcRepo", "destRepo");
            if (_config.Destination.Anonymize)
            {
                await AnonymizeDestRepo();
            }
            using (var destRepo = new GitRepository("destRepo"))
            {
                destRepo.Network.Remotes.Add("proxy", _ghDest.CloneUrl);
            }

            foreach (var branch in _config.Source.Branches)
            {
                await PushDestRepo(branch);
            }
        }

        private async Task ProxyReleases()
        {
            var srcReleases = await _ghClient.Repository.Release.GetAll(_config.Source.User, _config.Source.Repo);
        }

        private async Task PullSrcRepo(string branch)
        {
            Logger.Info($"Pulling branch {branch} from source repo...");

            var options = new PullOptions
            {
                FetchOptions = new FetchOptions {CredentialsProvider = GitCredentials}
            };
            await Task.Run(() =>
            {
                using var repo = new GitRepository("srcRepo");

                if (repo.Branches[branch] == null)
                {
                    var remoteBranch = repo.Branches[$"origin/{branch}"];
                    var newBranch = repo.CreateBranch(branch, remoteBranch.Tip);
                    repo.Branches.Update(newBranch, b => b.TrackedBranch = remoteBranch.CanonicalName);
                }

                Commands.Checkout(repo, repo.Branches[branch]);
                Commands.Pull(repo, _defaultSignature, options);
            });
        }

        private async Task PushDestRepo(string branch)
        {
            Logger.Info($"Pushing branch {branch} to destionation repo...");

            var options = new PushOptions {CredentialsProvider = GitCredentials};
            await Task.Run(() =>
            {
                using var repo = new GitRepository("destRepo");
                var remote = repo.Network.Remotes["proxy"];
                repo.Network.Push(remote, $"refs/heads/{branch}", options);
            });
        }

        private async Task AnonymizeDestRepo()
        {
            Logger.Info("Anonymizing commits...");

            var options = new RewriteHistoryOptions
            {
                CommitHeaderRewriter = commit =>
                {
                    var signature = new Signature(_defaultSignature.Name, _defaultSignature.Email,
                        commit.Committer.When);
                    return new CommitRewriteInfo
                        {Author = signature, Committer = signature, Message = commit.Message};
                }
            };
            await Task.Run(() =>
            {
                using var repo = new GitRepository("destRepo");
                repo.Refs.RewriteHistory(options, repo.Commits);
            });
        }
    }
}
