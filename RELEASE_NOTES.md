# Qubic.Net.Toolkit Release Note v0.6.0

> [!NOTE]
> **This is beta software.** Errors may occur — use with caution.

> [!IMPORTANT]
> **Seed Safety:** The Toolkit never shares or sends your seed to the network. Your seed is only held locally in memory while the application runs. Close the app when not actively using it. Qubic will never contact you to ask for your seed — **DO NOT SHARE your seed with anyone.**

## What is Qubic.Net Toolkit?

A cross-platform desktop application for interacting with the Qubic network. Runs as a native desktop window on Windows, macOS, and Linux — or as a local web app in your browser with `--server` mode.

## What's new in v0.6.0

- 🛡️ **Pre-broadcast confirmation modal** — every action that signs and sends a transaction now opens a preview dialog showing the amount, target tick, destination, and InputType before the seed signs anything. Applies to 21 transaction-sending pages across Send / Swap / Stake / Contracts / Tools.
- 🎯 **Quottery: correct invocation rewards on every procedure** — fixes the silent-rejection bug where actions like `PublishResult`, `AddToAskOrder`, etc. were sending `0 QU` to a contract that requires a specific amount. Toolkit now reads the live `BasicInfo` and sends `mDepositAmountForDispute` / `mAntiSpamAmount` / `ceil(duration_days) × mFeePerDay` etc. as required per-procedure.
- 🏷️ **Quottery: tag ID on Create Event** — packs a `uint16` tag into bytes 126–127 of the description slot, matching `qubic-cli`'s `quotteryCreateEvent`. Known IDs (Crypto / QUBIC / BTC / ETH / SOL) plus custom u16 support. Browse Active Events decodes the tag back to a name.
- 🏷️ **Sidebar version footer** — toolkit version is pinned at the bottom of the navigation, read at startup from the assembly.
- 🧰 **Internals**: `Qubic.Net` deps consumed as the NuGet package `Qubic.Services 1.4.1` (pulls Core / Crypto / Network / Rpc / Bob 1.5.1 / Serialization transitively). The `deps/Qubic.Net` submodule was removed in 0.5.0 — local development against unpublished packages uses the local-feed instructions in `NuGet.config`.

## Highlights

- **Native desktop window** — powered by Photino.Blazor using the OS webview (WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux)
- **Three backend options** — connect via official RPC, QubicBob JSON-RPC, or direct TCP to a Qubic node
- **Single-file binary** — self-contained, no .NET runtime required
- **Fully offline capable** — all CSS and fonts are bundled locally, no CDN or internet required for the UI

## Features

**Wallet & Transactions**
- **Send QU's** (single and batch)
- Burn QU, IPO bidding, custom transaction builder
- **Offline transaction** builder for air-gapped signing
- Message signing and verification
- Transaction history and tracking with **auto-resend**
- **Pre-broadcast confirmation modal** — preview every signed transaction (amount, destination, InputType) before it leaves the wallet

**Smart Contracts**
- Interactive contract browser with auto-discovered functions and procedures
- DeFi suite: Qx, QSwap, QEarn, QBond, Quottery
- Utilities: QUtil, MSVault, Nostromo, QVault
- **Quottery**: order-book betting (place/remove ask & bid), create events with tag IDs, publish/finalize results, dispute flow — all with contract-correct invocation rewards

**Explorer**
- Balance lookup and asset portfolio
- Transaction and transfer history lookup
- Tick data, computor list, active IPOs
- Transaction inclusion verification

**Tools**
- Identity generator (seed to public key)
- Broadcast pre-signed transactions
- Crypto toolkit (hashing, key derivation)
- **Oracle machine** queries
- Bob API playground

**Computor Operations** (RPC / Direct Network)
- Governance participation
- CCF performance metrics
- Node peer management

## Download

| Platform | File |
|----------|------|
| Windows x64 | `Qubic.Net.Toolkit-0.6.0-win-x64.zip` |
| macOS Apple Silicon (M1/M2/M3/M4) | `Qubic.Net.Toolkit-0.6.0-osx-arm64.zip` |
| macOS Intel | `Qubic.Net.Toolkit-0.6.0-osx-x64.zip` |
| Linux x64 | `Qubic.Net.Toolkit-0.6.0-linux-x64.zip` |

### Verify your download

> [!IMPORTANT]
> Always verify the SHA-256 hash against the checksums below to ensure the binary has not been tampered with:

```bash
# Windows (PowerShell)
Get-FileHash Qubic.Net.Toolkit-0.6.0-win-x64.zip -Algorithm SHA256

# macOS / Linux
sha256sum Qubic.Net.Toolkit-0.6.0-*.zip
```

| File | SHA-256 |
|------|---------|
| `Qubic.Net.Toolkit-0.6.0-win-x64.zip` | `cae7cce41fc18715b1fa995bb66fa078802d3d4135a4978963ab243086ae3d92` |
| `Qubic.Net.Toolkit-0.6.0-osx-arm64.zip` | `d87eea6e7ea07d384fa1b988f3619212870cebb13a2e33141e22a2cae1f5f9dc` |
| `Qubic.Net.Toolkit-0.6.0-osx-x64.zip` | `eb59a9c4bd90532dad85a8a685e2fdf501b0b2d6543cd4bd9ff3c6c6de020797` |
| `Qubic.Net.Toolkit-0.6.0-linux-x64.zip` | `8e7207247eb4bd9faff95725d10d0c3e642fd7ce5a028e30af5ac010270ed2c6` |

### Running

Each zip extracts into a `Qubic.Net.Toolkit-{platform}` folder.

**Windows:** Extract `Qubic.Net.Toolkit-0.6.0-win-x64.zip`, open the folder, and run `Qubic.Net.Toolkit.exe`

**macOS**:

> [!NOTE]
> Pre-built macOS binaries only support **server mode** (`--server`). For native desktop window mode, [compile from source](https://github.com/qubic/Qubic.Net/tree/main/tools/Qubic.Toolkit#running-from-source).

Download `osx-arm64` for Apple Silicon (M1/M2/M3/M4) or `osx-x64` for Intel Macs.

```bash
unzip Qubic.Net.Toolkit-0.6.0-osx-arm64.zip
cd Qubic.Net.Toolkit-0.6.0-osx-arm64
chmod +x Qubic.Net.Toolkit
codesign --force --deep -s - Qubic.Net.Toolkit
xattr -d com.apple.quarantine Qubic.Net.Toolkit
./Qubic.Net.Toolkit --server
```

**Linux:**

Desktop mode requires **GLIBC 2.38+** and **WebKitGTK** (`libwebkit2gtk-4.1-0`).

| Distribution | Version | Desktop Mode | Server Mode |
|---|---|---|---|
| Ubuntu | 24.04+ (Noble) | Yes | Yes |
| Debian | 13+ (Trixie) | Yes | Yes |
| Fedora | 39+ | Yes | Yes |
| Arch Linux | Rolling | Yes | Yes |
| Ubuntu | 22.04 (Jammy) | No | Yes |
| Debian | 12 (Bookworm) | No | Yes |

```bash
# Install WebKitGTK (Ubuntu/Debian)
sudo apt install libwebkit2gtk-4.1-0

unzip Qubic.Net.Toolkit-0.6.0-linux-x64.zip
cd Qubic.Net.Toolkit-0.6.0-linux-x64
chmod +x Qubic.Net.Toolkit
./Qubic.Net.Toolkit
```

If desktop mode is not supported on your system, the app automatically falls back to server mode.

**Server mode** (all platforms — opens in browser, no GLIBC 2.38 or WebKitGTK required):
```
./Qubic.Net.Toolkit --server
```
