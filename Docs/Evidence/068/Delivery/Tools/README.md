# 068 打包及独立验包工具快照

此处保留本次实际执行工具的原字节。打包工具默认只读预检，只有`--execute`写入三个不存在的ZIP及包外清单；独立工具只读核验既有ZIP，拒绝覆盖回执。两者都不构建、运行Unity、上传、推送或更改运行源码。

原执行环境是仓库`/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project`和隔离输出`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.11.0/Candidate3`。工具按该环境绑定路径；其原字节快照并非新的可移植打包接口。实际命令、源码快照、三包成员与SHA在包外Delivery回执中记录。

原工作副本位于Candidate3/Packaging。打包工具使用`--source-commit <已冻结源码快照>`；独立工具使用`--manifest Candidate3/Packaging/release-manifest.json --package-dir Candidate3 --repo <仓库> --output Candidate3/Packaging/independent-zip-verification.json`。两者在读取前后核对输入，源包绑定Git blob、类型与执行位；独立验证器不以打包器生成的清单自洽代替源码检查。
