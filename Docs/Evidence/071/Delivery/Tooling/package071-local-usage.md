# 071 本地打包脚本

此脚本尚未对正式项目、客户端或画廊运行。等原生退出、最终证据与文档完成并提交后，再传入那个明确提交。

```sh
python3 -B /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.14.0/Candidate1/Diagnostics/package071_local.py \
  --source-snapshot-commit FINAL_FULL_COMMIT_HASH \
  --repo /Users/Apple_501/Documents/Codex/work/frontier-reckoning/project \
  --app-directory /absolute/path/FrontierReckoning.app \
  --gallery-directory /absolute/path/gallery \
  --empty-output-directory /absolute/path/new-empty-delivery
```

`source-snapshot-commit` 必须是完整 40/64 位提交哈希；不接受 HEAD、分支或标签。源码直接使用 `git archive` 该提交，与之后 HEAD 是否漂移无关。三个输入须为真实目录；输出须在输入目录之外，且是新建或真实空目录。脚本从不删除已有内容。

输出为 `source.zip`、`client.zip`、`gallery.zip` 和 `package-manifest.json`。

- 源码的预期清单独立取自该提交的完整 Git 树及 blob 字节，再与 `git archive` 的 ZIP 比对。若属性导致 export-ignore/export-subst 略去或改写内容，或含未归档内容的 gitlink，脚本拒绝宣称完整通过。
- 客户端与画廊先从原目录读取 `lstat` 清单。使用目录描述符和 `O_NOFOLLOW` 逐文件读写；目录型符号链接也只存链接文本，绝不递归或读取其目标，即使目标在外部。空目录和 Unix mode 均保留；前后清单必须相同。
- 第二次独立打开每个 ZIP，完整读取各成员并重新计算 SHA/size，核无重名或路径别名、无绝对或穿越路径、无漏项或多项、type/mode/link 文本均正确。实际相等才写 `passed: true` 并退出 0。
- 精确清单记录每个 member 的 path/type/SHA-256/size/mode；源码额外标注 gitTreeMode。Git ZIP 对普通非执行文件及目录使用隐含 DOS 默认模式（原始 mode 字段为 0），执行文件和链接有显式 Unix mode；客户端、画廊使用原始 Unix mode。
- 清单也记录整个 ZIP 的实际 SHA-256 与 bytes。ACL、xattrs、资源分叉及纳秒时间未编码或验证；不据此声称复制了完整文件系统元数据。

失败会保留已生成 ZIP，并写 `passed: false` 和错误原因，方便核对；不会自动修补或删除原目录。此处只证明本地归档内容和字节一致，不证明原生画面、构建身份或 Google Drive 上传后的远端字节一致。

脚本不读取或写入 070 的固定目录，也不上传、修改 Git 工作树或要求额外批准。调用者应使用此次 071 的明确输入路径与独立空输出目录。
