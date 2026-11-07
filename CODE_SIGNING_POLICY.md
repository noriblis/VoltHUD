# Code signing policy

Status: SignPath Foundation application being prepared. Acceptance and certificate provisioning are pending. The published 0.1.1 preview is unsigned; this policy does not claim it has been signed.

Planned attribution upon approval: Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

## Scope and responsibilities

- Author, committer and reviewer: [noriblis](https://github.com/noriblis).
- Signing approver: [noriblis](https://github.com/noriblis).
- Outside contributions require maintainer review. Signing requests require explicit maintainer approval in SignPath. Team members must use multi-factor authentication for GitHub and SignPath; onboarding must confirm this before production signing is enabled.
- Only VoltHUD.exe built from this repository's source on GitHub-hosted Windows Actions runners may be submitted. Arbitrary local executables and third-party binaries are outside the signing scope.
- The artifact configuration restricts the executable's name, product, company and version metadata. Signing credentials stay in protected GitHub Actions secrets, never in the app or repository.
- Signed output must have a valid Authenticode signature, the configured publisher, a timestamp and the expected version before packaging. Failed or unavailable signing stops the signed-release workflow; it never substitutes an unsigned executable.

The signing workflow runs only by manual dispatch on main. It produces a downloadable Actions artifact by default; publication additionally requires selecting the publish option. Published files are not modified after signing; checksums are calculated after packaging.

## Privacy

VoltHUD does not transfer information to other networked systems. It reads local device information and stores preferences and bounded, hashed alert history locally. Optional Windows notifications contain device names and reported battery percentages. Diagnostics are written locally only when requested; users decide whether to share them. See [SECURITY.md](SECURITY.md) and the README's privacy section.

Development-time GitHub Actions signing sends the repository's build artifact and associated build metadata to SignPath for verification and signing. This service is not embedded in VoltHUD and does not collect app users' device information. See [SignPath's privacy policy](https://signpath.io/privacy-policy).

## Licensing

Project code and supplied project artwork are distributed under the MIT license. Artwork was supplied by the maintainer and generated with ChatGPT; see [assets/README.md](assets/README.md). Windows/.NET system libraries are used but not bundled.
