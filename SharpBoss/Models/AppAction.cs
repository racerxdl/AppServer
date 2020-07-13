using System;

namespace SharpBoss.Models {
  /// <summary>
  /// The Application class
  /// </summary>
  internal class AppAction {
    private string _appName;
    private bool _isRemoved;

    /// <summary>
    /// Gets the application name
    /// </summary>
    public string ApplicationName {
      get { return this._appName; }
      set { this._appName = value; }
    }

    /// <summary>
    /// Gets if application is removed
    /// </summary>
    public bool IsRemoved {
      get { return this._isRemoved; }
      set { this._isRemoved = value; }
    }

    /// <summary>
    /// Instantiate new AppAction with default values
    /// </summary>
    public AppAction () : this (null, false) { }

    /// <summary>
    /// Instantiate new AppAction
    /// </summary>
    /// <param name="appName">Application name</param>
    /// <param name="remove">Is removed?</param>
    public AppAction (string appName, bool isRemoved) {
      this._appName = appName;
      this._isRemoved = isRemoved;
    }
  }
}
