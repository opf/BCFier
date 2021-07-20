using System;
using CefSharp;

namespace OpenProject.WebViewIntegration
{
  public sealed class DownloadHandler : IDownloadHandler
  {
    public event EventHandler<DownloadItem> OnBeforeDownloadFired;

    public event EventHandler<DownloadItem> OnDownloadUpdatedFired;

    public void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
      IBeforeDownloadCallback callback)
    {
      OnBeforeDownloadFired?.Invoke(this, downloadItem);

      if (callback.IsDisposed) return;
      using (callback) callback.Continue(downloadItem.SuggestedFileName, false);
    }

    public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
      IDownloadItemCallback callback)
    {
      OnDownloadUpdatedFired?.Invoke(this, downloadItem);
    }
  }
}
