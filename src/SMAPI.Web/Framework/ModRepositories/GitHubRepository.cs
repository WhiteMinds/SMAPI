using System;
using System.Threading.Tasks;
using StardewModdingAPI.Internal.Models;
using StardewModdingAPI.Web.Framework.Clients.GitHub;

namespace StardewModdingAPI.Web.Framework.ModRepositories
{
    /// <summary>An HTTP client for fetching mod metadata from GitHub project releases.</summary>
    internal class GitHubRepository : RepositoryBase
    {
        /*********
        ** Properties
        *********/
        /// <summary>The underlying GitHub API client.</summary>
        private readonly IGitHubClient Client;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="vendorKey">The unique key for this vendor.</param>
        /// <param name="client">The underlying GitHub API client.</param>
        public GitHubRepository(string vendorKey, IGitHubClient client)
            : base(vendorKey)
        {
            this.Client = client;
        }

        /// <summary>Get metadata about a mod in the repository.</summary>
        /// <param name="id">The mod ID in this repository.</param>
        public override async Task<ModInfoModel> GetModInfoAsync(string id)
        {
            // validate ID format
            if (!id.Contains("/") || id.IndexOf("/", StringComparison.InvariantCultureIgnoreCase) != id.LastIndexOf("/", StringComparison.InvariantCultureIgnoreCase))
                return new ModInfoModel($"The value '{id}' isn't a valid GitHub mod ID, must be a username and project name like 'Pathoschild/LookupAnything'.");

            // fetch info
            try
            {
                // get latest release
                GitRelease latest = await this.Client.GetLatestReleaseAsync(id, includePrerelease: true);
                GitRelease preview = null;
                if (latest == null)
                    return new ModInfoModel("Found no mod with this ID.");

                // get latest stable release (if not latest)
                if (latest.IsPrerelease)
                {
                    preview = latest;
                    latest = await this.Client.GetLatestReleaseAsync(id, includePrerelease: false);
                }

                // return data
                return new ModInfoModel(name: id, version: this.NormaliseVersion(latest?.Tag), previewVersion: this.NormaliseVersion(preview?.Tag), url: $"https://github.com/{id}/releases");
            }
            catch (Exception ex)
            {
                return new ModInfoModel(ex.ToString());
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public override void Dispose()
        {
            this.Client.Dispose();
        }
    }
}
