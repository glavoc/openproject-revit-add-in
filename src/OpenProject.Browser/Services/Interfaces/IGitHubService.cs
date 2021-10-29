using OpenProject.Browser.Models;
using Optional;

namespace OpenProject.Browser.Services
{
  public interface IGitHubService
  {
    Option<Release> GetLatestRelease();
  }
}
