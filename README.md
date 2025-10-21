# RG-Adguard Microsoft Store Downloader

A lightweight Windows app that lets you **generate and download direct Microsoft Store package links** using the [store.rg-adguard.net](https://store.rg-adguard.net/) API.  
Built in **.NET 8 WinForms**, no dependencies, no telemetry — just paste a Store URL and grab the `.appx`, `.msix`, or `.appxbundle` files directly.

---

## Features
- Paste a **Microsoft Store URL**, **Product ID**, **PackageFamilyName**, or **Category ID**  
- Choose the **ring** (`Retail`, `RP`, `WIF`, `WIS`) and fetch download links  
- Automatically parses and lists all available packages  
- Filter to only `.appx` / `.msix` formats  
- Multi-select and download with progress tracking  
- Double-click any entry to open the raw link in your browser  
- Portable — works standalone (single EXE build option)

---

## Building
1. Install [.NET 8 SDK](https://dotnet.microsoft.com/download)
2. Clone the repo  
   ```bash
   git clone https://github.com/YOUR_USERNAME/RG-Adguard-Downloader.git
   cd RG-Adguard-Downloader
   ```
3. Build:
   ```bash
   dotnet build -c Release
   ```
4. Or publish a self-contained EXE:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
   ```

The final EXE will appear in:
```
bin\Release\net8.0-windows\win-x64\publish\
```

---

## How It Works
The app posts to  
`https://store.rg-adguard.net/api/GetFiles`  
with the same parameters used on the rg-adguard site (type, value, ring, lang) to fetch Microsoft CDN download links.  
These are **official time-limited URLs** for `.appx`, `.msix`, and related package types.

---

## Disclaimer
This project is for **educational and archival purposes only**.  
Please respect Microsoft’s and rg-adguard’s terms of use.  
Do not automate excessive requests or redistribute proprietary content.
