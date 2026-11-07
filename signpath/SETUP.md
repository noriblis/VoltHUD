# SignPath onboarding and release integration

Current status: application preparation; no SignPath account, approval, token or certificate is assumed. Existing GitHub releases remain unsigned.

1. Complete the Foundation application using the public repository, releases and Code signing policy. Do not claim established adoption; this is a newly released project. Confirm asset licensing and accept the provider terms yourself or give explicit confirmation at submission.
2. After acceptance, enable MFA for both GitHub and SignPath. Assign noriblis as the signing approver; configure explicit approval for every production signing request.
3. Configure the GitHub.com trusted build system for noriblis/VoltHUD, using GitHub-hosted runners only. If SignPath requests its GitHub App, review its requested access and limit it to VoltHUD. Installation/access approval is a separate owner action.
4. Create the project, artifact configuration and production signing policy using the values supplied by SignPath. Import artifact-configuration.xml. Require product and file versions from the build's version parameter, and retain the explicit file/product/company restrictions.
5. Create a narrowly scoped CI signing token through SignPath's supported onboarding process. Store it as the repository Actions secret SIGNPATH_API_TOKEN. Never paste it into chat, a workflow file or a commit.
6. Set repository Actions variables SIGNPATH_ORGANIZATION_ID, SIGNPATH_PROJECT_SLUG, SIGNPATH_SIGNING_POLICY_SLUG, SIGNPATH_ARTIFACT_CONFIGURATION_SLUG and SIGNPATH_EXPECTED_PUBLISHER. Use the actual approved certificate's simple publisher name; do not guess it. Set SIGNPATH_ENABLED=true only after all prerequisites are verified.
7. Update CODE_SIGNING_POLICY.md and the README/SECURITY status after acceptance, keeping the earlier unsigned preview accurately documented. Manually run Signed Windows preview on main. Leave publish=false for the first request. Approve the request in SignPath and inspect the resulting signed artifact and verification output.
8. For a new published version, update app/package versions and documentation together. Choose a new tag and publish=true only after reviewing the source. The existing v0.1.1-preview tag must not be replaced. The proposed v0.1.1-preview.1 tag can distinguish a signed build of this version without rewriting its unsigned release.

The workflow uploads only the just-built VoltHUD.exe as an immutable Actions artifact, submits that artifact ID, verifies the downloaded signature and timestamp, then replaces the packaging input with the signed file. Checksums are computed after packaging. A separate publish job receives only the signed release artifact. Missing configuration or signing failure stops the run.

The current release workflow remains available for unsigned builds while the application is pending. It does not claim successful signing. No signed workflow has been submitted or tested against SignPath until an approved account and configuration exist.

Official references: [Foundation conditions](https://signpath.org/terms), [GitHub integration](https://docs.signpath.io/trusted-build-systems/github), [artifact metadata restrictions](https://docs.signpath.io/artifact-configuration/examples).
