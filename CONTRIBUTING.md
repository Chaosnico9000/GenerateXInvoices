# Contributing to Invoice Generator

Thank you for your interest in contributing! ??

---

## ?? Getting Started

### Prerequisites
- **.NET 10 SDK** or later
- **Visual Studio 2024** or **Visual Studio Code** (with C# extension)
- **Git** for version control

### Setup Development Environment

1. **Clone repository**
   ```bash
   git clone https://github.com/yourusername/InvoiceGenerator.git
   cd InvoiceGenerator
   ```

2. **Restore packages**
   ```bash
   dotnet restore
   ```

3. **Build solution**
   ```bash
   dotnet build
   ```

4. **Run application**
   ```bash
   dotnet run
   # Or use: start.bat (Windows) or .\quick-build.ps1 (PowerShell)
   ```

---

## ?? Project Structure

```
InvoiceGenerator/
??? Program.cs              # Main application logic
??? GenerateXInvoices.csproj  # Project file
??? build.ps1               # Build automation script
??? quick-build.ps1         # Quick build menu
??? start.bat               # Windows quick-start
??? Resources/              # Icons and assets
?   ??? app.ico
?   ??? README.md
??? Properties/
?   ??? PublishProfiles/    # Deployment profiles
??? README.md               # Documentation
??? CHANGELOG.md            # Version history
??? DEPLOYMENT-SUMMARY.md   # Deployment guide
??? ICON-GUIDE.md           # Icon creation guide
??? RELEASE-CHECKLIST.md    # Release process
```

---

## ??? Development Workflow

### 1. Branch Naming Convention
- **Feature**: `feature/your-feature-name`
- **Bugfix**: `bugfix/issue-number-description`
- **Hotfix**: `hotfix/critical-issue`
- **Docs**: `docs/update-readme`

### 2. Commit Messages
Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add export to PDF functionality
fix: resolve CSV export encoding issue
docs: update deployment guide
perf: optimize XML parsing with XmlReader
refactor: extract validation logic to separate class
test: add unit tests for invoice generation
```

### 3. Pull Request Process

1. **Fork** the repository
2. **Create** a feature branch from `main`
3. **Commit** your changes with clear messages
4. **Test** your changes locally
5. **Push** to your fork
6. **Open** a Pull Request with:
   - Clear description of changes
   - Reference to related issue (if applicable)
   - Screenshots/videos for UI changes

---

## ? Code Standards

### C# Coding Style
We follow the [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).

Key points:
- **Indentation**: 4 spaces (no tabs)
- **Braces**: On new line (Allman style)
- **Naming**: PascalCase for public members, camelCase for private fields
- **Nullability**: Enable `<Nullable>enable</Nullable>`

### EditorConfig
The project includes `.editorconfig` which enforces:
- UTF-8 encoding
- Line endings (CRLF for Windows)
- Trailing whitespace removal
- Consistent indentation

### Code Analysis
- **Zero warnings** policy for Release builds
- Fix all `dotnet format` issues before committing
- Run code cleanup: `dotnet format`

---

## ?? Testing Guidelines

### Manual Testing
Before submitting PR, test:
1. Generate 1 invoice (DateTime.Now)
2. Generate 10,000 invoices (batch)
3. Export CSV
4. Search functionality
5. Settings persistence
6. Language switching (EN/DE)

### Performance Benchmarks
Ensure no regressions:
- Invoice generation: > 1,000/sec
- CSV export: > 3,000/sec
- Startup time: < 300ms (Single-File build)

### Build Validation
```powershell
# Debug build
dotnet build -c Debug

# Release build
dotnet build -c Release

# Format check
dotnet format --verify-no-changes
```

---

## ?? Documentation

### Code Comments
- **Public APIs**: XML documentation comments
- **Complex logic**: Inline comments explaining "why", not "what"
- **Performance-critical sections**: Document optimizations

Example:
```csharp
/// <summary>
/// Generates invoices in parallel using object pooling for optimal performance.
/// </summary>
/// <param name="count">Number of invoices to generate</param>
/// <param name="settings">Generation settings</param>
/// <returns>Collection of generated invoices</returns>
public static IEnumerable<Invoice> GenerateParallel(int count, GeneratorSettings settings)
{
    // Use Partitioner for better work-stealing in parallel loops
    var partitioner = Partitioner.Create(0, count, batchSize: 128);
    
    // ...implementation
}
```

### README Updates
Update `README.md` for:
- New features (add to Features section)
- Changed configuration options
- New dependencies
- Breaking changes

---

## ?? UI/UX Contributions

### Spectre.Console Guidelines
- Use consistent color scheme:
  - **Cyan**: Primary actions
  - **Green**: Success states
  - **Yellow**: Warnings
  - **Red**: Errors/Destructive actions
  - **Grey**: Informational text

- Maintain professional design (no emojis in production UI)
- Test with both light and dark terminal themes
- Ensure proper alignment for tables and panels

### Localization (i18n)
When adding new strings:
1. Add key to `L.T()` dictionary (both EN and DE)
2. Use placeholders for dynamic values: `L.T("Invoice count: {0}", count)`
3. Test with both locales

Example:
```csharp
// In L._dict["en"]:
["Export completed"] = "Export completed"

// In L._dict["de"]:
["Export completed"] = "Export abgeschlossen"

// Usage:
AnsiConsole.MarkupLine($"[green]{L.T("Export completed")}[/]");
```

---

## ?? Performance Contributions

### Optimization Checklist
- [ ] Profile with `dotnet-trace` or BenchmarkDotNet
- [ ] Measure before/after with concrete numbers
- [ ] Document benchmark results in PR
- [ ] Avoid premature optimization (prove bottleneck exists)

### Accepted Optimizations
? Parallel processing improvements  
? Memory allocation reduction  
? I/O buffering enhancements  
? Algorithm complexity improvements  
? Caching strategies

### Avoid
? Micro-optimizations without profiling  
? Unsafe code (unless absolutely necessary)  
? Platform-specific hacks  
? Readability sacrifices for negligible gains

---

## ?? Bug Reports

### Good Bug Report Includes:
1. **Environment**:
   - OS version (Windows 10/11, version)
   - .NET version (`dotnet --version`)
   - Build type (Portable, Single-File, etc.)

2. **Steps to Reproduce**:
   - Exact menu selections
   - Input values
   - Settings configuration

3. **Expected vs. Actual Behavior**

4. **Logs/Screenshots**:
   - Console output
   - Error messages
   - Stack traces

### Use GitHub Issue Template
```markdown
**Bug Description**
A clear description of the bug.

**To Reproduce**
1. Go to '...'
2. Select '...'
3. Enter value '...'
4. See error

**Expected Behavior**
What you expected to happen.

**Environment**
- OS: Windows 11 23H2
- .NET: 10.0.1
- Build: Single-File (v1.0.0)

**Screenshots/Logs**
(if applicable)
```

---

## ?? Feature Requests

### Good Feature Request Includes:
- **Problem statement**: What problem does this solve?
- **Proposed solution**: How should it work?
- **Alternatives**: What other approaches were considered?
- **Impact**: Who benefits? How many users?

### Before Submitting
- [ ] Search existing issues/PRs
- [ ] Check if feature aligns with project goals
- [ ] Consider if it can be a plugin/extension

---

## ?? Deployment Contributions

### Build Scripts
- Test on clean Windows 10/11 VMs
- Support both PowerShell Core and Windows PowerShell
- Include error handling and rollback logic

### MSIX Packaging
- Validate with Windows App Certification Kit
- Test installation on fresh Windows instances
- Document certificate requirements

---

## ?? Recognition

Contributors will be:
- Listed in `CHANGELOG.md` for each release
- Credited in release notes
- Mentioned in project README (for significant contributions)

---

## ?? Questions?

- **GitHub Discussions**: For general questions
- **GitHub Issues**: For bug reports
- **Email**: support@example.com (for private inquiries)

---

## ?? License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

**Thank you for making Invoice Generator better! ??**
