module.exports = {
  branches: [
    "main",
    { name: "dev", channel: "beta", prerelease: "beta" }
  ],
  tagFormat: "v${version}",
  plugins: [
    [
      "@semantic-release/commit-analyzer",
      { preset: "conventionalcommits" }
    ],
    [
      "@semantic-release/release-notes-generator",
      { preset: "conventionalcommits" }
    ],
    [
      "@semantic-release/exec",
      {
        prepareCmd: "bash .github/scripts/build-release-assets.sh ${nextRelease.version}"
      }
    ],
    [
      "@semantic-release/github",
      {
        assets: [
          {
            path: "artifacts/olrx-win-x64.zip",
            name: "olrx-${nextRelease.gitTag}-win-x64.zip",
            label: "olrx Windows x64"
          }
        ]
      }
    ]
  ]
};
