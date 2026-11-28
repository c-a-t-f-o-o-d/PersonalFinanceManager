namespace PersonalFinanceManager.App.UI
{
    public enum PageScrollMode
    {
        Viewport,
        Page
    }

    public interface IPageLayoutControl
    {
        int PreferredPageHeight { get; }
        PageScrollMode ScrollMode => PageScrollMode.Viewport;
    }
}
