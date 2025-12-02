# ?? Professional Invoice Generator

A high-performance, multi-language invoice generator with advanced analytics, built with .NET 10 and C# 14.

---

## ? Features

### Core Functionality
- ? **High-Speed Generation**: Generate 1000+ invoices/second with parallel processing
- ?? **Multi-Language Support**: English, German (extensible)
- ?? **Dual Format Export**: XML + optional JSON sidecar files
- ?? **Advanced Analytics**: Statistics, aging reports, validation, top customers
- ?? **Beautiful CLI**: Powered by Spectre.Console with professional UI

### Technical Highlights
- ?? **.NET 10** with C# 14 latest features
- ?? **Optimized I/O**: Buffered streams, parallel processing, object pooling
- ?? **Configurable**: Persistent settings, customizable locales, currencies, VAT rates
- ?? **Scalable**: Handles 100,000+ invoices effortlessly

---

## ?? Quick Start

### Prerequisites
- **.NET 10 Runtime** (or SDK for development)
- **Windows 10/11** (optimized for Windows Desktop)

### Run from Source
```powershell
dotnet run
```

### Build Profiles

#### ?? Development Build (Fast)
```powershell
.\quick-build.ps1
# Select option [1]
```
Output: `publish\win-x64\` (requires .NET 10 runtime)

#### ?? Production Build (Self-Contained, Single File)
```powershell
.\quick-build.ps1
# Select option [2]
```
Output: `publish\win-x64\InvoiceGenerator.exe` (~15-20 MB)
- ? No runtime required
- ? Single executable
- ? Optimized & trimmed

#### ?? Portable Build (Minimal Size)
```powershell
.\quick-build.ps1
# Select option [3]
```
Output: `publish\portable\` (~500 KB + runtime dependency)

#### ?? ARM64 Build (Surface Pro X, etc.)
```powershell
.\quick-build.ps1
# Select option [4]
```

---

## ?? Icon Setup

### Create/Replace Icon
1. Place your `app.ico` file in the `Resources\` folder
2. Rebuild the project

### Recommended Icon Sizes
- 16×16, 32×32, 48×48, 256×256 (multi-resolution .ico)

### Generate Icon from PNG
```powershell
# Using ImageMagick
magick convert icon.png -define icon:auto-resize=256,128,64,48,32,16 app.ico
```

---

## ?? MSIX Packaging (Microsoft Store / Enterprise)

### Prerequisites
- **Windows SDK** (includes MakeAppx.exe and SignTool.exe)
- **Code Signing Certificate** (for distribution)

### Package Creation

#### 1. Build Production Version
```powershell
.\build.ps1 -Configuration Release -Runtime win-x64 -SelfContained -CreateMSIX
```

#### 2. Create MSIX Package
```powershell
# Navigate to SDK tools
cd "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"

# Create package
.\MakeAppx.exe pack /d "C:\path\to\publish\msix" /p "InvoiceGenerator.msix"

# Sign package (requires certificate)
.\SignTool.exe sign /fd SHA256 /a /f "certificate.pfx" /p "password" "InvoiceGenerator.msix"
```

#### 3. Install Package
```powershell
Add-AppxPackage -Path "InvoiceGenerator.msix"
```

### Self-Signing Certificate (Development Only)
```powershell
# Create test certificate
New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=YourCompany" `
    -KeyUsage DigitalSignature -FriendlyName "Invoice Generator" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Export certificate
$cert = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert | Select-Object -First 1
Export-PfxCertificate -Cert $cert -FilePath "test-cert.pfx" -Password (ConvertTo-SecureString -String "test123" -Force -AsPlainText)
```

---

## ?? Advanced Deployment

### IIS / Web Deploy
```powershell
# Build for web deployment
.\build.ps1 -Configuration Release -Runtime win-x64 -SelfContained

# Copy to web server
xcopy /E /Y publish\win-x64 \\server\inetpub\InvoiceGenerator\
```

### Docker Container
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY publish/win-x64 .
ENTRYPOINT ["./InvoiceGenerator"]
```

### ClickOnce Deployment
```powershell
# Enable ClickOnce in .csproj
msbuild /t:Publish /p:Configuration=Release /p:PublishUrl="https://deploy.example.com/"
```

---

## ?? Configuration

### Settings File Location
```
%APPDATA%\GenerateXInvoices\settings.json
```

### Example Configuration
```json
{
  "locale": "de",
  "currency": "EUR",
  "defaultVatRate": 0.19,
  "fixedItemCount": null,
  "seed": 42,
  "saveJsonSidecar": false,
  "prettyPrintXml": false,
  "invoicePrefix": "INV",
  "minLineItems": 100,
  "maxLineItems": 500,
  "paymentTermsDays": [7, 14, 30]
}
```

---

## ??? Performance Tuning

### Recommended Build Flags
```xml
<PublishTrimmed>true</PublishTrimmed>
<PublishReadyToRun>true</PublishReadyToRun>
<TieredCompilation>true</TieredCompilation>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

### Benchmarks (AMD Ryzen 9 / 16 Threads)
| Operation | Speed | Notes |
|-----------|-------|-------|
| Invoice Generation | 1,200/sec | Parallel, 500 items/invoice |
| CSV Export | 3,500/sec | Streaming I/O |
| XML Parsing | 2,800/sec | XDocument optimized |

---

## ?? License

MIT License - See LICENSE file for details

---

## ?? Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing`)
5. Open Pull Request

---

## ?? Support

For issues or questions, please open a GitHub issue or contact support@example.com

---

**Built with ?? using .NET 10 and Spectre.Console**
