namespace AppServer.Server {
  class AppAction {
    private string appName;
    private bool remove;

    public string ApplicationName {
      get { return appName; }
      set { appName = value; }
    }

    public bool ApplicationRemoved {
      get { return remove; }
      set { remove = value; }
    }

    public AppAction() {

    }

    public AppAction(string name, bool remove) {
      this.appName = name;
      this.remove = remove;
    }
  }
}
