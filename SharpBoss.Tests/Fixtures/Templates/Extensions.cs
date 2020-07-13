namespace SharpBoss.Tests.Fixtures.Templates {
  public partial class WebServerTemplate : WebServerTemplateBase {
    public string Namespace { get; set; }
    public string Class { get; set; }
  }

  public partial class ConfigFileTemplate : ConfigFileTemplateBase {
    public string ListenUrl { get; set; }
  }
}
