<h1 align="center">
  <img src="https://github.com/user-attachments/assets/f8736a10-7ee4-4f29-ad3d-b8891843cae7" height="128" width="128" alt="Logo" />
  <br />
  Saeros
</h1>

<div>
  <h2 align="center">An open source HIDS tailored for Microsoft Windows and Active Directory</h2>
  <p align="center">Enrich your SIEM for threat intelligence, forensics and UEBA (User & entity behavior analytics).</p>
</div>

## Purpose

Saeros is neither a SIEM nor an EDR. Instead, it sits between the two, delivering the contextual insights that most platforms overlook. Its purpose is to detect common user behaviors that could indicate data exfiltration, infiltration, malware activity, or other malicious or suspicious actions.

## Key takeaways

- **High-performance processing**: Ingests **tens of thousands of Windows Event Logs per second** with minimal CPU usage.
- **Low bandwidth footprint**: Executes detection logic **locally on each host**, sending only matched detections over gRPC.
- **Automatic audit policy management**: **Dynamically configures audit policies** based on the rules you enable.
- **Extensive ruleset**: Ships with **thousands of curated Sigma rules** from the official [Sigma](https://github.com/SigmaHQ/sigma) repository.
- **Customizable rules**: Modify any rule at runtime to **fit your environment and requirements**.
- **Flexible detection exclusions**: **Exclude detections** using one or multiple event properties.
- **Powerful integrations**: Forward detections directly to **ElasticSearch**.
- **MITRE ATT&CK visibility**: Explore detection coverage by **tactic**, **technique**, or **sub-technique**.
- **Effortless AD deployment**: Install agents on **domain controllers with a single click**.
- **Air-gapped ready**: Fully operational **without internet access**.
- **Zero external dependencies**: Requires **no DBMS or third-party components**.
- **Fast, simple installation**: Get started in **just a few clicks**.

### Use cases

Saeros can detect thousands of suspicious activities, including:

- Repeated failed logon attempts (password-guessing attacks)
- Sudden spikes in network traffic from a single process (data exfiltration)
- Remote logins originating from public IP addresses (lateral movements)
- Users being created or added to sensitive user groups (privilege escalation)
- Event logs being cleared (defense evasion)

## Deployment

Saeros can be deployed on a standalone Microsoft Windows endpoint and across Microsoft Active Directory domains. The setup installs two Windows services and a desktop application.

**Saeros Collector (Agent)** – A Windows service that starts automatically with the system. It configures audit policies, subscribes to ETW channels, performs Sigma rule matching, and forwards detections to the *Bridge*. For simplicity, this service is referred to as the *Agent*. It usually sits on a standalone Windows workstation or on domain controllers.

**Saeros Collector (Bridge)** – A Windows service that also starts automatically with the system. It communicates with the *Agent*, manages Sigma rule configuration, stores detections in a local SQLite database, and forwards them to configured SIEMs. The *Bridge* exposes an API consumed by the *Console*. It usually sits on the local installation (where the installer was run).

**Saeros Console** – A Windows desktop application used to manage Sigma rules and exclusions, browse detections, configure integrations, and deploy collectors to Active Directory domains. It usually sits on the local installation (where the installer was run).

---

### Non Domain-Joined Environment

In a non domain-joined (standalone) environment, the installer deploys both services and the *Console* on the local machine. Saeros immediately begins collecting events, and detections become available in the *Console*. An *Agent* can still be deployed to a domain later to monitor domain controllers.

#### Requirements

This is the simplest setup: no network or firewall configuration is required, and all communication occurs locally. The *Saeros Collector (Bridge)* service exposes a local HTTP endpoint and uses local gRPC communication with both the *Agent* and the *Console*.

---

### Domain-Joined Environment

In a domain-joined environment, the installer detects domain membership and can deploy an *Agent* to each domain controller during setup. Deployment occurs via a Group Policy Object (GPO). Each *Agent* forwards detections back to the primary domain controller through a gRPC named pipe, then to the local *Saeros Collector (Bridge)* service (where the installer was run).

#### Requirements

Deployment must be performed by a user in the **Builtin Administrators** group. The password is not stored and is used only to establish SMB/LDAP connections during deployment.

The following ports must be open, and firewall rules must allow connections from the *Saeros Collector (Bridge)* service to the primary domain controller:

- **TCP/445**
- **TCP/389** (or **TCP/686** for LDAPS)

Deployment creates a GPO with the following components:

- Protected folder (read-only access for Authenticated Users):  
  **C:\Windows\SYSVOL\domain\Policies\{3560FF19-45A3-4F9A-956B-937A04D2AABF}**
- A scheduled task that installs the *Saeros Collector (Agent)* service on domain controllers
- The *Collector.exe* signed binary which is executed by all domain controllers as a Windows service under LocalSystem identity
- **Audit.csv** containing required audit policies based on configured rules
- Registry values enabling the required ETW channels
- ADMX templates that configure PowerShell policies, including:  
  *Include command line in process creation events*,  
  *Configure Logon Script Delay*,  
  *Turn on Module Logging*,  
  *Turn on PowerShell Script Block Logging*

**Note:** Only detections—not full event logs—are sent from the *Agent* to the *Bridge*, significantly reducing bandwidth requirements.

#### Diagrams
| Standalone           | Active Directory |
| ------------- | ------------- |
| <img src="https://github.com/user-attachments/assets/4b5119d3-0302-4605-bf3e-0abd52e56ad5" data-canonical-src="https://github.com/user-attachments/assets/4b5119d3-0302-4605-bf3e-0abd52e56ad5" width="500" />  | <img src="https://github.com/user-attachments/assets/652dbdde-746c-47fc-bf93-758fa83c1b11" data-canonical-src="https://github.com/user-attachments/assets/652dbdde-746c-47fc-bf93-758fa83c1b11" width="500" />  |

## Installation

Saeros is quick and straightforward to install. Download Saeros-Setup.exe from the [Releases](https://github.com/Saeros-Security/Saeros/releases/tag/v1.0.0) page and follow the guided setup.

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
