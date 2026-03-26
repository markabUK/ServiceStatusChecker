namespace ServiceStatusChecker.Models;


public class EmailConfig
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string SmtpServer { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}