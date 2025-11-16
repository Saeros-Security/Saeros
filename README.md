<h1 align="center">
  <img src="https://github.com/user-attachments/assets/f8736a10-7ee4-4f29-ad3d-b8891843cae7" height="128" width="128" alt="Logo" />
  <br />
  Saeros
</h1>

<div>
  <h2 align="center">An open source HIDS tailored for Microsoft Active Directory and Workstations</h2>
  <p align="center">Enrich your SIEM for threat intelligence, forensics and UEBA (User & entity behavior analytics).</p>
</div>

## Purpose

Saeros is neither a SIEM nor an EDR. Instead, it sits between the two, delivering the contextual insights that most platforms overlook. Its purpose is to detect common user behaviors that could indicate data exfiltration, infiltration, malware activity, or other malicious or suspicious actions.

## Key takeaways

- **High-performance processing**: Handles **tens of thousands of Windows Event Logs per second** with negligible CPU impact.
- **Lower bandwidth usage**: Detection logic runs **locally on each host**, sending only matches over gRPC.
- **Seamless audit policies configuration**: **Automatically updates audit policies** depending on enabled rules.
- **Extensive detection ruleset**: Includes **thousands of curated Sigma rules** from the official [Sigma](https://github.com/SigmaHQ/sigma) repository.
- **Effortless AD integration**: Deploys to **domain controllers in a single click**.
- **Air-gapped ready**: Fully functional **without any internet connectivity**.
- **Zero external dependencies**: **No DBMS or third-party components** required.
- **Fast, simple installation**: Up and running in **four clicks**.

### Use cases

Saeros can detect thousands of suspicious activities, including:

- Repeated failed logon attempts (password-guessing attacks)
- Sudden spikes in network traffic from a single process (data exfiltration)
- Remote logins originating from public IP addresses (lateral movements)
- Users being created or added to sensitive user groups (privilege escalation)
- Event logs being cleared (defense evasion)

## Features

- Real-time detection based on 2000+ Sigma rules
- Secure and powerful user interface
- Automatic audit policies configuration
- Sigma rule import
- Rule exclusions
- ElasticSearch integration (Slack/SYSLOG/SMTP in a next release)
- Mitre Att&ck mapping

## Built-in Sigma rules

The default rules are derived from a curated subset of the official [Sigma](https://github.com/SigmaHQ/sigma) rule repository.

## Deployments

Saeros can be deployed on a standalone Windows endpoint or across Microsoft Active Directory domains.

### Standalone deployment

The collector runs as a Windows service that subscribes to relevant ETW channels and performs real-time Sigma rule matching. Each detection is stored locally and then forwarded to any configured integration such as Elasticsearch or other supported SIEMs.

### Active Directory deployment

The collector is deployed to domain controllers via a Group Policy Object (GPO) and forwards all detections to the machine where Saeros is installed.

## Performances

Saeros has been tested on domain controllers processing over 20,000 events per second, maintaining a minimal resource footprint of under 5% CPU and ~200 MB of memory.

## Screenshots

### Home

<img width="3072" height="1831" alt="Home-1" src="https://github.com/user-attachments/assets/2650ccea-c4b1-4581-86d3-9fbd48fd45f0" />

<img width="3072" height="1837" alt="Home-2" src="https://github.com/user-attachments/assets/9044d3e6-b7cb-496c-88e9-83b4a8ec50c5" />

<img width="3072" height="1837" alt="Home-3" src="https://github.com/user-attachments/assets/4a2f1e83-ff59-4d25-bd93-210b3d3c3d4d" />

### Detections

<img width="3072" height="1831" alt="Detections-1" src="https://github.com/user-attachments/assets/6e931bdb-9635-4c25-b07e-6e59ddb1b3e4" />

<img width="3072" height="1837" alt="Detections-2" src="https://github.com/user-attachments/assets/e4b655ec-2a27-443e-9cf6-1a167f649e0d" />

### Rules

<img width="3072" height="1829" alt="Rules-1" src="https://github.com/user-attachments/assets/04a9c3d8-68cb-4157-a646-184ad27abfb8" />

<img width="3072" height="1834" alt="Rules-2" src="https://github.com/user-attachments/assets/ced5aada-9daf-4209-aae5-f6167d7f75d3" />

### Mitre

<img width="3072" height="1829" alt="Rules-1" src="https://github.com/user-attachments/assets/63cb8cd4-b698-4689-80a2-89f08c2bf3b0" />

### Integrations

<img width="3072" height="1829" alt="Integrations-1" src="https://github.com/user-attachments/assets/328b183e-8aa7-419a-ab6e-cdbe6eb826a0" />

### Settings

<img width="3072" height="1831" alt="Settings-1" src="https://github.com/user-attachments/assets/637addf7-5f82-45b3-b692-317380b86373" />

<img width="3072" height="1831" alt="Settings-2" src="https://github.com/user-attachments/assets/a8616506-2cc9-4d0e-9c64-c721b377f287" />

## Installation

Saeros is fast and easy to setup. Download Saeros-Setup.exe in the [Releases](https://github.com/Saeros-Security/Saeros/releases/tag/v1.0.0) section and follow the steps.
