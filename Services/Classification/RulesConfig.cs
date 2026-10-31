//Catalog of Provider rules for secret detection
//Populates ProviderRule records

using System;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;



public class RuleConfig
{
    private static ProviderRule Rule(
        string provider,
        string prefix,
        string credentialType,
        string pattern)

    {
    return new ProviderRule(
        provider,
        prefix,
        credentialType,
        new Regex( 
            pattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100))
        );
    }

    public static IEnumerable<ProviderRule> GetRules()
    {

        // Anthropic
        yield return Rule(
            "Anthropic", "sk-ant-api03-", "API Key",
            @"\Ask-ant-api03-[A-Za-z0-9_-]{93}AA\z");

        yield return Rule(
            "Anthropic", "sk-ant-admin01-", "Admin API Key",
            @"\Ask-ant-admin01-[A-Za-z0-9_-]{93}AA\z");

        // PyPI
        yield return Rule(
            "PyPI", "pypi-", "Upload Token",
            @"\Apypi-[A-Za-z0-9_-]{85,}\z");

        // Hugging Face
        yield return Rule(
            "Hugging Face", "hf_", "Access Token",
            @"\Ahf_[A-Za-z]{34}\z");

        // SendGrid
        yield return Rule(
            "SendGrid", "SG.", "API Key",
            @"\ASG\.[A-Za-z0-9_=.-]{66}\z");

        // DigitalOcean
        yield return Rule(
            "DigitalOcean", "dop_v1_", "Personal Access Token",
            @"\Adop_v1_[a-f0-9]{64}\z");

        yield return Rule(
            "DigitalOcean", "doo_v1_", "OAuth Access Token",
            @"\Adoo_v1_[a-f0-9]{64}\z");

        yield return Rule(
            "DigitalOcean", "dor_v1_", "OAuth Refresh Token",
            @"\Ador_v1_[a-f0-9]{64}\z");

        // Grafana
        yield return Rule(
            "Grafana", "glc_", "Cloud API Token",
            @"\Aglc_[A-Za-z0-9+/]{32,400}={0,3}\z");

        yield return Rule(
            "Grafana", "glsa_", "Service Account Token",
            @"\Aglsa_[A-Za-z0-9]{32}_[A-Fa-f0-9]{8}\z");

        // Pulumi
        yield return Rule(
            "Pulumi", "pul-", "API Token",
            @"\Apul-[a-f0-9]{40}\z");

        // Shopify
        yield return Rule(
            "Shopify", "shpat_", "Access Token",
            @"\Ashpat_[A-Fa-f0-9]{32}\z");

        // 1Password
        yield return Rule(
            "1Password", "ops_", "Service Account Token",
            @"\Aops_eyJ[A-Za-z0-9+/]{250,}={0,3}\z");

        // Age encryption
        yield return Rule(
            "Age", "AGE-SECRET-KEY-1", "Private Key",
            @"\AAGE-SECRET-KEY-1[QPZRY9X8GF2TVDW0S3JN54KHCE6MUA7L]{58}\z");

        // Shippo
        yield return Rule(
            "Shippo", "shippo_live_", "Live API Token",
            @"\Ashippo_live_[A-Fa-f0-9]{40}\z");

        // GitHub — Personal Access Tokens
        yield return Rule(
            "GitHub", "ghp_", "Classic Personal Access Token",
            @"\Aghp_[A-Za-z0-9]{36}\z");

        yield return Rule(
            "GitHub", "github_pat_", "Fine-grained Personal Access Token",
            @"\Agithub_pat_\w{82}\z");

        // GitHub — OAuth Tokens
        yield return Rule(
            "GitHub", "gho_", "OAuth Access Token",
            @"\Agho_[A-Za-z0-9]{36}\z");

        yield return Rule(
            "GitHub", "ghu_", "OAuth User-to-Server Token",
            @"\Aghu_[A-Za-z0-9]{36}\z");

        // GitHub — App Installation Tokens
        yield return Rule(
            "GitHub", "ghs_", "Installation Access Token",
            @"\Aghs_[A-Za-z0-9]{36}\z");

        // GitHub — Refresh Tokens
        yield return Rule(
            "GitHub", "ghr_", "Refresh Token",
            @"\Aghr_[A-Za-z0-9]{36}\z");
    }
}
