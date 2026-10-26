<div align="center">

[![Latest Release](https://img.shields.io/github/v/release/mehdirzfx/ADB-Manager?style=for-the-badge&logo=github&color=3DDC84)](https://github.com/mehdirzfx/ADB-Manager/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/mehdirzfx/ADB-Manager/total?style=for-the-badge&logo=windows&color=0078D6)](https://github.com/mehdirzfx/ADB-Manager/releases)
[![Stars](https://img.shields.io/github/stars/mehdirzfx/ADB-Manager?style=for-the-badge&logo=github&color=yellow)](https://github.com/mehdirzfx/ADB-Manager/stargazers)
[![Forks](https://img.shields.io/github/forks/mehdirzfx/ADB-Manager?style=for-the-badge&logo=github)](https://github.com/mehdirzfx/ADB-Manager/network/members)
[![Issues](https://img.shields.io/github/issues/mehdirzfx/ADB-Manager?style=for-the-badge&logo=github)](https://github.com/mehdirzfx/ADB-Manager/issues)

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows)
![Last Commit](https://img.shields.io/github/last-commit/mehdirzfx/ADB-Manager?style=flat-square)

</div>

# ADB Manager — Wireless ADB GUI for Windows (Connect, Pair with QR, Port Scan)

**ADB Manager** is a lightweight Windows desktop app (C# / .NET 8 WinForms) that makes **Android Debug Bridge (ADB)** easy to use over Wi-Fi. Connect your phone by IP, pair it by scanning a **QR code**, auto-discover it with **mDNS**, switch from **USB to Wi-Fi** in one click, and manage the ADB server — all from a clean GUI instead of typing `adb` commands.

If you use **Android Wireless debugging**, `adb pair`, `adb connect` or `adb tcpip` often, this tool saves you time.

> Author: [mehdirzfx](https://github.com/mehdirzfx)

---

## Features

- **Wireless ADB connect** — connect to a phone by `IP:port` (default port `5555`).
- **Pair with QR code** — generates a QR code you scan from *Developer options → Wireless debugging → Pair device with QR code* (Android 11+). The app finds the pairing service via mDNS and finishes pairing automatically.
- **Manual pairing** — pair using `ip:port` and the 6-digit pairing code.
- **Auto connect (mDNS)** — discovers devices that have Wireless debugging enabled on the same Wi-Fi network and connects to them.
- **Port scanner** — Android 11+ uses a random port for wireless debugging. The built-in scanner checks `5555` and `30000–50000` and connects to the first working ADB port.
- **USB → Wi-Fi** — switches a USB-connected device to TCP/IP mode (`adb tcpip 5555`), reads its Wi-Fi IP and connects.
- **Device manager** — list devices, disconnect selected / all, reboot selected device.
- **ADB server control** — start, kill and restart the ADB server, with a live status for port `5037`.
- **Stop any operation** — every button turns into **Stop** while it runs, so a long port scan or a pending QR pairing can be cancelled at any time.
- **Remembers `adb.exe`** — finds ADB automatically (saved path, `PATH`, `ANDROID_HOME`, `ANDROID_SDK_ROOT`, default Android SDK folder) or lets you browse for it.
- **Compact, grouped UI** — settings are organised in group boxes with a small log panel.

## Requirements

- Windows 10 / 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (not needed if you use a self-contained build)
- `adb.exe` from the official [Android SDK Platform-Tools](https://developer.android.com/tools/releases/platform-tools)
- Android device with **Developer options** enabled. Wireless debugging and QR pairing require **Android 11 or newer**; the PC and phone must be on the same network.

## Download

Get the latest build from the [**Releases**](https://github.com/mehdirzfx/ADB-Manager/releases) page, extract it and run `ADB Manager.exe`.

## Quick start

1. Run **ADB Manager**. If `adb.exe` is not found, click **Browse...** and select it (inside the `platform-tools` folder).
2. On the phone: **Settings → Developer options → Wireless debugging → On**.
3. Pick one of the ways to connect:
   - **Pair with QR** → on the phone choose *Pair device with QR code* and scan the QR shown in the app.
   - **Auto connect (mDNS)** → connects to devices found on the network.
   - **Connect** → enter the phone IP and port, or use **Scan ports** if you don't know the port.
4. Your device appears in the **Devices** list. Use the buttons next to it to disconnect or reboot.

### Switch from USB to Wi-Fi

Connect the phone with a USB cable, select it in the device list and click **USB -> Wi-Fi**. The app enables TCP/IP mode and connects over Wi-Fi; you can then unplug the cable.

## Build from source

```bash
git clone https://github.com/mehdirzfx/ADB-Manager.git
cd ADB-Manager
dotnet build -c Release
```

Create a single-file Windows build:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The project uses [QRCoder](https://github.com/codebude/QRCoder) for QR generation.

## FAQ

**Does it replace `adb`?**
No. ADB Manager is a graphical front-end that calls your existing `adb.exe`.

**QR pairing doesn't find my phone.**
Make sure Wireless debugging is on, both devices are on the same Wi-Fi network, and the network allows mDNS (some guest / public networks block it). You can use manual pairing or **Scan ports** instead.

**Which port does wireless debugging use?**
On Android 11+ it is chosen randomly (shown in the Wireless debugging screen). Use **Scan ports** if you don't want to read it from the phone.

## Keywords

adb, adb gui, adb manager, android debug bridge, wireless adb, adb wifi, adb pair, adb connect, adb qr code, adb mdns, adb port scanner, android wireless debugging, usb to wifi adb, adb tcpip, windows, winforms, csharp, dotnet

---

Made by [mehdirzfx](https://github.com/mehdirzfx)