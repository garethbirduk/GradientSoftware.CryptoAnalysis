[<img src="https://raw.githubusercontent.com/garethbirduk/GradientSoftware.REPONAME/main/resources/icon.png" width="25" height="25">](https://github.com/garethbirduk/GradientSoftware.REPONAME)
[![main](https://github.com/garethbirduk/GradientSoftware.REPONAME/actions/workflows/main.yml/badge.svg)](https://github.com/garethbirduk/GradientSoftware.REPONAME/actions)
[![coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/garethbirduk/GIST_ID/raw/code-coverage.json)](https://garethbirduk.github.io/GradientSoftware.REPONAME)

# Template for dotnet projects

- [ ] GH_APIKEY - Generate this in developer settings including package creation / deletion<br>
- [ ] GIST_AUTH_TOKEN - Generate this in developer settings including gist creation
- [ ] GISTID - Create a gist and get the GISTID in this step
- [ ] REPONAME - Repo name (without GradientSoftware.) e.g. Utils

## Generate coverage gist
https://gist.github.com/
- [ ] Set Gist description - eg code-coverage-utils.json
- [ ] Set Gist filename - eg code-coverage-utils.json
- [ ] Set Gist content - eg code-coverage-utils.json
- [ ] Note the GISTID

# Folder level Search and Replace in files
- [ ] REPONAME
- [ ] GISTID - note GISTID is the mask; GIST_ID is the reference to the environment variable which must remain as GIST_ID not the value!

# Rename files and folders
- [] /REPONAME
- [] /REPONAME/REPONAME.csproj
- [] /REPONAME.Test
- [] /REPONAME.Test/REPONAME.Test.csproj
- [] /REPONAME.sln
      
# Set environment variables
https://github.com/garethbirduk/GradientSoftware.REPONAME/settings/secrets/actions
- [ ] GH_APIKEY
- [ ] GIST_AUTH_TOKEN
- [ ] GIST_ID

# Set rules
https://github.com/garethbirduk/GradientSoftware.REPONAME/settings/rules
- [ ] Main protection

# Github pages
https://github.com/garethbirduk/GradientSoftware.REPONAME/settings/pages
- [ ] Build and deployment source: Github Actions
- [ ] Configure static page then Cancel. Not sure this is required.

