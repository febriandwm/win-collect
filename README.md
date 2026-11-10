# win-collect

A native Windows desktop application that collects keyboard activity and synchronizes it to Firebase Cloud Firestore.

> **For research and educational purposes only.**

## Features

- 🖥️ Native Windows WPF application
- ⌨️ System-wide keyboard activity collection
- ☁️ Firebase Firestore synchronization
- 🔄 Offline queue with automatic retry
- 📦 SQLite local storage
- 🔔 System tray background operation
- 🚀 Windows startup option
- 🖥️ Multi-computer support

## Technology Stack

| Component | Technology |
|---|---|
| Language | C# |
| Framework | .NET 10 |
| UI | WPF |
| Cloud | Firebase Firestore |
| Auth | Firebase Anonymous Auth |
| Local DB | SQLite |
| Logging | Serilog |
| CI/CD | GitHub Actions |

## Project Structure

```
win-collect/
├── .github/workflows/     # CI/CD pipelines
├── src/win-collect/       # WPF application
│   ├── Infrastructure/    # App state, logging
│   ├── Models/            # Data models
│   ├── Services/          # Business logic
│   ├── ViewModels/        # MVVM ViewModels
│   └── Resources/         # Icons
├── tests/                 # Unit tests
├── installer/             # WiX installer
└── docs/                  # Documentation
```

## Building

Requirements:
- .NET 10 SDK
- Windows 10/11

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test
dotnet publish src/win-collect/win-collect.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish
```

## Setup

1. Enable Anonymous Authentication in Firebase Console
2. Deploy Firestore Security Rules (see `docs/firebase-setup.md`)
3. Build and run the application
4. Enter your computer name and click **Save**
5. Click **Start** to begin monitoring

## CI/CD

GitHub Actions automatically:
- Builds on every push/PR to `main`
- Creates a GitHub Release on `v*` tags
- Uploads self-contained executable

Create a release:
```bash
git tag v1.0.0
git push origin v1.0.0
```

## Privacy & Security

- Anonymous Firebase authentication
- Credentials are never stored in source code
- Only keyboard key names are recorded (no context/application data)
- Data is stored only in your Firebase project
