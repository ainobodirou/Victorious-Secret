# VictoriousSecret (VS) - GitHub secret scanner

## Disclaimer

This is a personal project implementing a naive secret-detection algorithm. It should not be treated or used as a replacement for industry-standard tooling.

A naive secret-detection API and CLI client for GitHub repositories, powered by secret provider regex pattern matching, contextual flags, and entropy-based scoring.

Built with .NET, Octokit, and GitHub Apps, with optional ngrok tunneling.

* Set up a custom GitHub App and run the API service locally or on a hosted server.
* Run scans manually on all authorized repositories for a selected installation through the CLI. Individual-repository scans are not implemented yet.
* Get automatic check runs for push events to the default branch of authorized repositories through webhooks.

## Minimal API setup - Local

Prerequisites:

* .NET 10 SDK for running from source or installing the CLI tool.
* Docker with Compose for running the containerized backend.
* An ngrok account only if using the ngrok tunnel.

[Create a GitHub App](https://docs.github.com/en/apps/creating-github-apps/registering-a-github-app/registering-a-github-app) with minimum settings:

* Repository permissions: **Contents: Read-only** and **Checks: Read and write**.
* Enable device flow for CLI authentication.
* For automatic scans, activate webhooks and subscribe to **Push** events once the public webhook URL is available. Webhooks can remain inactive during initial setup.
* Install the GitHub App on the repositories you want to scan.

Record the **App ID** for the backend and the **client ID** for the CLI. Generate and download the app's private key (`.pem`). Choose a webhook secret and save the same value in GitHub and the backend configuration; the secret is independent of the webhook URL.

Use [appsetting_example.json](appsetting_example.json) as the backend configuration template. From the repository root, choose one setup:

* Native execution: copy it to root `appsettings.json`. Set `GitHubApp:AppId`, `GitHubApp:WebhookSecret`, and `GitHubApp:PrivateKeyPath` to your values. Use an absolute path to the private key on your machine.
* Compose: copy it to `deploy/appsettings.json`, put the private key at `deploy/secrets/github-app.pem`, and keep `PrivateKeyPath` as `/run/secrets/github-app.pem`, the path inside the container. Set your App ID and webhook secret.

The current backend requires a webhook secret at startup even if webhook delivery is inactive. Keep private keys and populated configuration files out of version control.

## Webhook configuration and push events

To create check runs on pushes to the default branch, the GitHub App needs a publicly reachable webhook URL ending in `/api/github/webhooks`.

Use the supplied ngrok Compose service, your own hosted HTTPS address, or another tunnel/proxy. The backend does not need its public URL in its configuration.

### Custom proxy or hosted endpoint

Configure your proxy or hosting platform to forward requests to the backend's `/api/github/webhooks` route. Supply your GitHub App with the public webhook URL and the same webhook secret used by the backend:

```text
https://your-domain.example/api/github/webhooks
```

The default Compose port is bound to `127.0.0.1:5000`. A proxy on another host or Docker network needs suitable connectivity to the backend.

### ngrok

For Compose, copy [.env.example](.env.example) to `.env` at the repository root and set `NGROK_AUTHTOKEN` to your [ngrok authtoken](https://dashboard.ngrok.com/get-started/your-authtoken). This token authenticates ngrok and is separate from the webhook secret.

Start the backend with the ngrok profile using the Docker command below. Find the active public endpoint in the [ngrok dashboard](https://dashboard.ngrok.com/endpoints), append `/api/github/webhooks`, and save the full URL in your GitHub App's webhook settings. Then activate webhook delivery and subscribe to Push events. Starting the tunnel does not update GitHub automatically.

For native execution, install and authenticate the [ngrok agent](https://ngrok.com/download), start the backend, and run this in a separate terminal:

```sh
ngrok http 5000 --traffic-policy-file deploy/ngrok-policy.yml
```

Use its displayed HTTPS URL plus `/api/github/webhooks` in GitHub. The supplied policy restricts public forwarding to POST requests on that route. If the public URL changes, update the URL in GitHub; the webhook secret stays the same.

## Running the backend API

### Local

After configuring root `appsettings.json`, run from the repository root:

```sh
dotnet run --project SecAudit.csproj --urls http://localhost:5000
```

Native execution does not automatically load the Compose `.env` file or start ngrok.

### Docker

After configuring `deploy/appsettings.json` and the private key, run from the repository root.

With a custom proxy or hosted webhook endpoint (or for CLI scans without webhooks):

```sh
docker compose -f deploy/compose.yaml up -d --build
```

With the ngrok tunnel and the populated root `.env`:

```sh
docker compose --env-file .env -f deploy/compose.yaml --profile webhooks up -d --build
```

The `webhooks` profile starts ngrok as a separate container. Without the profile, only the backend starts; no ngrok credentials are needed.

## Minimal CLI setup

Set configuration in the shell where you will run the CLI. These commands use Bash/Zsh syntax and do not require a particular working directory:

```sh
export VS_API_URL="http://localhost:5000"
export VS_GITHUB_CLIENT_ID="your-github-app-client-id"
```

`VS_API_URL` defaults to `http://localhost:5000`. Override it if the backend uses another host or published port. `VS_GITHUB_CLIENT_ID` defaults to the VictoriousSecret GitHub App's client ID; override it when using your own GitHub App. The client ID is public configuration, not a client secret.

The installed CLI does not automatically read `.env` or the backend's `appsettings.json`. For a hosted backend, point `VS_API_URL` to an address exposing the scan API routes; the supplied ngrok policy exposes only the webhook route.

You can run the CLI from source at the repository root:

```sh
dotnet run --project VictoriousSecret.Cli -- --help
```

Or install it as a .NET tool from NuGet:

```sh
dotnet tool install --global VictoriousSecret.Cli
vs --help
```

Ensure the .NET global tools directory is on your `PATH` if `vs` is not found.

## Commands

The VS CLI currently supports:

```sh
vs auth login
vs scan --all
vs auth logout
```

Log in before scanning. `scan --all` prompts you to select a GitHub App installation and scans its accessible repositories.

The commands `vs auth status` and `vs scan --repo owner/repository` are advertised in help but are not implemented yet.

Denis Lisovskij, November 2026

Version 0.1.0
