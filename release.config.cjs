module.exports = {
  branches: [
    "main",
    { name: "dev", channel: "beta", prerelease: "beta" }
  ],
  tagFormat: "v${version}",
  plugins: [
    [
      "@semantic-release/commit-analyzer",
      {
        preset: "conventionalcommits",
        releaseRules: [
          { type: "chore", scope: "deps", release: "patch" }
        ]
      }
    ],
    [
      "@semantic-release/release-notes-generator",
      {
        preset: "conventionalcommits",
        presetConfig: {
          types: [
            { type: "feat", section: "Features" },
            { type: "feature", section: "Features" },
            { type: "fix", section: "Bug Fixes" },
            { type: "perf", section: "Performance Improvements" },
            { type: "revert", section: "Reverts" },
            { type: "chore", scope: "deps", section: "Dependency Updates" },
            { type: "chore", scope: "deps-dev", section: "Dependency Updates" },
            { type: "chore", scope: "deps-ci", section: "Dependency Updates" }
          ]
        }
      }
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
