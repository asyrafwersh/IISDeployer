# IISDeployer

A Windows desktop application (WPF/.NET) for streamlined deployment of IIS (Internet Information Services) websites and applications over a network using SMB (Server Message Block) file sharing.

---

## 📋 About the Project

**IISDeployer** is a configurable deployment tool designed to simplify the process of deploying web applications to IIS servers in enterprise or multi-environment setups (e.g., UAT, Production). It provides a guided, step-by-step workflow for connecting to remote servers, transferring files, and managing IIS websites and application pools — all through an intuitive graphical interface.

The tool is especially useful for teams that manage multiple environments and need a reliable, repeatable deployment process without relying on complex CI/CD pipelines.

---

## ✨ Features

- **Configurable Deployment** — Define and save multiple deployment configurations, each with their own network path, credentials, IIS website name, app pool, source folders, and environment/config files.
- **Multi-Section Deployment** — Support for multiple deployment sections per configuration (e.g., Frontend, API), each independently configurable.
- **SMB Network Connection** — Connect and disconnect to SMB network shares with saved credential management. Transfer files directly to the remote deployment folder.
- **IIS Website & App Pool Control** — Start/stop IIS websites and application pools on the target server directly from the UI.
- **File & Folder Selection** — Select specific files and folders as deployment artifacts, with support for a parent source folder.
- **Zip & Stage** — Create date-stamped subfolders (DDMMYYYY) and zip selected items into a staging folder before transfer.
- **Saved Configurations** — Save, load, and auto-fill credentials for multiple server configurations (e.g., App1 UAT, Mi CCTV Server, Office File Server, Backup Storage).
- **Application Settings** — Manage all saved network configurations and deployment sections from a dedicated Settings tab. Rename, reorder, or remove navigation bar tabs.
- **Env/Config File Support** — Specify environment configuration files (e.g., `.env`) per deployment section.

---

## 🖥️ Screenshots

### Configurable Deployment

<img width="1919" height="1199" alt="Screenshot 2026-03-08 152943" src="https://github.com/user-attachments/assets/9dced1d4-27b1-4c82-934b-a49f5de8207e" />


### SMB Network Connection

<img width="1919" height="916" alt="Screenshot 2026-03-08 152953" src="https://github.com/user-attachments/assets/3e7df3e5-f8e8-4e5c-9a5c-569cce59b90c" />


### Application Settings

<img width="1914" height="1193" alt="Screenshot 2026-03-08 153001" src="https://github.com/user-attachments/assets/c53ee1d8-5e26-4bca-a1a6-42b95ef9062c" />


---

## 🚀 Getting Started

### Prerequisites

- Windows OS
- .NET Framework / .NET (WPF)
- Access to a target IIS server via SMB network share

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/asyrafwersh/IISDeployer.git
   ```
2. Open `IISDeployer.sln` in Visual Studio.
3. Build and run the solution.

---

## 🛠️ Usage

1. **Configure Settings** — Go to the **Settings** tab to create a new network configuration. Fill in the Name, Network Path (e.g., `\\Server1\DeploymentFolder`), Username, and Password.
2. **Add Deployment Sections** — Within each configuration, add sections (e.g., Frontend, API) with the corresponding IIS Website Name, App Pool Name, and source files/folders.
3. **Connect via SMB** — Switch to the **SMB Connection** tab, select a saved configuration to auto-fill credentials, then click **Connect**.
4. **Deploy** — Use the **Configurable Deploy** tab to select a configuration, pick the files/folders to deploy, zip them, and transfer to the server.
5. **Manage IIS** — Start or stop IIS websites and app pools directly from the deploy section.

---

## 📁 Project Structure

```
IISDeployer/
├── IISDeployer/        # Main WPF application project
├── View/               # WPF Views (XAML)
├── IISDeployer.sln     # Visual Studio solution file
└── README.md
```

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Feel free to open an issue or submit a pull request.

---

## 📄 License

This project is open source. See the repository for license details.

---

> **Note : this project is an AI assisted project**
