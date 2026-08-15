# Apple Signing

The unsigned iOS workflow builds the app for the simulator and does not require Apple signing secrets.

To produce a signed IPA for your iPad, use `build-ios-signed.yml` and add these GitHub repository secrets:

- `APPLE_CERTIFICATE_P12_BASE64`
  - Base64-encoded `.p12` signing certificate export
- `APPLE_CERTIFICATE_PASSWORD`
  - password for the `.p12` file
- `APPLE_PROVISIONING_PROFILE_BASE64`
  - Base64-encoded `.mobileprovision` file
- `APPLE_PROVISIONING_PROFILE_SPECIFIER`
  - provisioning profile name/specifier used by Xcode
- `APPLE_DEVELOPMENT_TEAM`
  - your Apple Developer Team ID

## What Apple provides

You will eventually need:

- an Apple Developer Program membership
- a signing certificate
- a provisioning profile
- access to the bundle identifier used by the iOS app

## What the workflow does

- creates a temporary keychain
- imports the signing certificate
- installs the provisioning profile
- builds the generated Xcode project
- exports an IPA
- uploads the IPA artifact
- deletes the temporary signing material when the runner is cleaned up

## Important notes

- Do not commit certificates, provisioning profiles, or private keys.
- The repository only stores references to secret names.
- If you change the bundle identifier, update the provisioning profile mapping in the workflow too.

