using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace ShowMeTheWorkitems
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                
                /* User input*/
                string serverName = @"http://{serverName}:8080/tfs/DefaultCollection"; // Server URL needs to have the collection
                string repositoryName = "{repositoryName}";                            // Repository Name
                int pullRequestId = 0;                                                 // Pull request Id

                /* Get all the services needed */
                TfsTeamProjectCollection tfs = new TfsTeamProjectCollection(new Uri(serverName));
                GitHttpClient gitClient = tfs.GetClient<GitHttpClient>();
                ILinking linkingService = tfs.GetService<ILinking>();

                /* Get the repository object */
                GitRepository gitRepo = gitClient.GetRepositoriesAsync().Result.Find((repository) => repository.Name == repositoryName);
                gitRepo = gitClient.GetRepositoryAsync(gitRepo.Id).Result;

                /* Get the pull request object */
                GitPullRequest pullRequest = gitClient.GetPullRequestAsync(gitRepo.Id, pullRequestId).Result;

                /* Get all the commits in the pull request and build a Url List */
                List<GitCommitRef> commits = commits = gitClient.GetPullRequestCommitsAsync(gitRepo.Id, pullRequestId).Result;
                IEnumerable<string> commitUris = commits.Select((commit) =>
                {
                    string url = String.Format(CultureInfo.InvariantCulture,
                        "{0}/{1}/{2}",
                        gitRepo.ProjectReference.Id,
                        gitRepo.Id,
                        commit.CommitId);

                    return LinkingUtilities.EncodeUri(new ArtifactId("Git", ArtifactTypeNames.Commit, url));
                });

                /* Get all the work items linked to the commits */
                LinkFilter linkFilter = new LinkFilter();
                linkFilter.FilterType = FilterType.ToolType;
                linkFilter.FilterValues = new String[1] { ToolNames.WorkItemTracking };

                Artifact[] artifacts = linkingService.GetReferencingArtifacts(
                    commitUris.ToArray(),
                    new LinkFilter[1] { linkFilter });

                /* Print the list of workitems */
                Dictionary<int, string> workItemIds = GetWorkItemIds(artifacts);
                workItemIds.Keys.ToList().ForEach((id) => Console.WriteLine(id + "\t" + workItemIds[id]));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static Dictionary<int, string> GetWorkItemIds(IEnumerable<Artifact> artifacts)
        {
            if (null == artifacts)
            {
                return new Dictionary<int, string>();
            }

            Dictionary<int, string> ids = new Dictionary<int, string>();
            foreach (Artifact artifact in artifacts)
            {
                if (artifact == null)
                {
                    continue;
                }

                int workItemId = 0;
                string workItemTitle = string.Empty;

                foreach (ExtendedAttribute ea in artifact.ExtendedAttributes)
                {
                    if (String.Equals(ea.Name, "System.Id", StringComparison.OrdinalIgnoreCase))
                    {
                        Int32.TryParse(ea.Value, out workItemId);
                    }
                    else if (String.Equals(ea.Name, "System.Title", StringComparison.OrdinalIgnoreCase))
                    {
                        workItemTitle = ea.Value;
                    }

                    if (workItemId != 0 && !string.IsNullOrEmpty(workItemTitle))
                    {
                        break;
                    }
                }

                ids[workItemId] = workItemTitle;
            }

            return ids;
        }
    }
}