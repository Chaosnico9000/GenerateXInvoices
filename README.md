# Professional Invoice Generator

A high-performance, multi-language invoice generator with advanced analytics, built with .NET 10 and C# 14.

---

## Features

### Core Functionality
- **High-Speed Generation**: Generate up to 1000+ invoices/second with parallel processing
- **Multi-Language Support**: English, German (extensible)
- **Dual Format Export**: XML + optional JSON sidecar files
- **Advanced Analytics**: Statistics, aging reports, validation, top customers
- **Beautiful CLI**: Powered by Spectre.Console with professional UI

### Technical Highlights
- **.NET 10** with C# 14 latest features
- **Optimized I/O**: Buffered streams, parallel processing, object pooling
- **Configurable**: Persistent settings, customizable locales, currencies, VAT rates
- **Scalable**: Handles 100,000+ invoices effortlessly

---

## Quick Start

### Prerequisites
- **.NET 10 Runtime** (or SDK for development)
- **Windows 10/11** (optimized for Windows Desktop)

### Run from Source
```powershell
dotnet run
```

---

## Configuration

### Settings File Location
```
%APPDATA%\Roaming\GenerateXInvoices\settings.json
```

### Default Configuration
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
  "MinUnitPrice": 2,
  "MaxUnitPrice": 1200,
  "paymentTermsDays": [7, 14, 30],
  "AutoIncrementInvoiceNumber": true,
  "DateFormat": "yyyy-MM-dd"
}
```

---

## Performance Tuning

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

## License

MIT License - See LICENSE file for details

---

## Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing`)
5. Open Pull Request

---

## Support

For issues or questions, please open a GitHub issue or contact me on Discord @Chaosnico9000

---

**Built with ❤️ using .NET 10 and Spectre.Console**

