namespace ProxySwitcher
{
    public partial class MainWindow
    {
        internal class Paramobject
        {
            public Paramobject(string url, int index)
            {
                this.Url = url;
                this.Index = index;
            }
            public string Url { get; private set; }
            public int Index { get; private set; }
        }

    }
}
