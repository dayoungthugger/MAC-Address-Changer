# MAC Address Changer

A small Windows tool for changing (spoofing) the MAC address of a network adapter. Pick an adapter, type in an address or let it generate a random one, and apply it.

## What it does

- **Change your MAC:** choose an adapter, enter or generate an address, and apply it.
- **Random addresses:** you get a random vendor prefix with a random tail. If you want to look like a particular manufacturer, you can pin it to a specific vendor OUI.
- **Restore the original:** this removes the override. The tool remembers each adapter's factory address between sessions, so you can always get back to it.
- **Persistent or temporary:** make the change survive reboots, or keep it temporary so it reverts on reboot or when the adapter restarts.
- **Shows every adapter:** the list includes the hidden and virtual ones too (WAN Miniports, Wi-Fi Direct, TAP, vEthernet, VPN adapters).

## Building

Just run:

```
build.cmd
```

It uses the `csc.exe` that already ships with Windows, so you don't need the SDK, Visual Studio, or NuGet. When it finishes, you'll find `MacChanger.exe` in the repo root.

## Using it

1. Run `MacChanger.exe` and accept the UAC prompt (it needs admin rights to edit the registry).
2. Select your adapter.
3. Type in a MAC address, or hit **Random**.
4. Click **Change MAC now**.

## How it works

The tool writes a `NetworkAddress` string value into the adapter's subkey under:

```
HKLM\SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}\00NN
```

The next time the driver initializes, NDIS uses that value instead of the burned-in address. That's why the adapter gets disabled and re-enabled after a change.

Adapters are matched to their subkey by `NetCfgInstanceId`, never by name, since names can change or be duplicated."# MAC-Address-Changer" 
