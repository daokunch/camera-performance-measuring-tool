public class Program
{
    [System.STAThreadAttribute()]
    public static void Main()
    {
        using (new MyUwpApp.App())
        {
            WpfAppWithUwpControl.App app = new WpfAppWithUwpControl.App();
            //app.InitializeComponent();
            app.Run();
        }
    }
}