namespace InfoSupport.StaticCodeAnalyzer.WebApp.Services;

public class PageTitleEventArgs(string title, string redirect) : EventArgs
{
    public string Title { get; set; } = title;
    public string Redirect { get; set; } = redirect;
}

public class NavBarStateService
{
    public string PageTitle { get; set; } = "";
    public string TitleRedirect { get; set; } = "/";

    public event EventHandler<PageTitleEventArgs>? OnPageTitleUpdate;

    public void UpdateTitle(string title, string redirect="/")
    {
        PageTitle = title;
        OnPageTitleUpdate?.Invoke(this, new PageTitleEventArgs(title, redirect));
    }
}
