/// <reference path="./.vscode/config.d.ts" />

const { project } = require("ucpem")

project.prefix("./InterEx/bin/Release/net9.0").res("InterEx.dll")
project.prefix("./InterEx.Modules/bin/Release/net9.0").res("InterEx.Modules.dll",
    project.ref("InterEx.dll")
)
