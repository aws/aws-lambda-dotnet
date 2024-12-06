namespace Amazon.Lambda.TestTool.Models;

public class ApplicationOptions
{
    public string Host { get; set; } = Constants.DefaultHost;

    public int Port { get; set; } = Constants.DefaultPort;

    public bool NoLaunchWindow { get; set; }

    public bool PauseExit { get; set; } = true;

    public bool DisableLogs { get; set; } = false;


    /// <summary>
    /// The directory to store in local settings for a Lambda project for example saved Lambda requests.
    /// </summary>
    public string GetPreferenceDirectory(bool createIfNotExist)
    {
        // TODO Figure out what the preference directory should be in V2.

        return Directory.GetCurrentDirectory();
    }
}
