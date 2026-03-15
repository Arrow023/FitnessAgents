namespace FitnessAgentsWeb.Core.Configuration
{
    public interface IAppConfigurationProvider
    {
        string GetAiModel();
        string GetAiKey();
        string GetAiEndpoint();
        string GetSmtpPassword();
        string GetFromEmail();
        string GetToEmail();
    }
}
