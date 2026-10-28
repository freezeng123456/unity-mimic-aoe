# 0.15.0 / 072 完整自然对战记录

云盘连接器的单文件上限为100MiB；原始对战ZIP为209,058,503字节，因此逐字节拆成三块上传。源码、客户端、证据与截图仍各自是完整ZIP。

下载 normal-routes-originals072.zip.part001、part002、part003，以及 normal072-transport-manifest.json 和 assemble-normal072.py，放入同一个本地目录，在该目录运行：

```sh
python3 assemble-normal072.py
```

脚本校验三个分块、合并结果SHA256及完整ZIP CRC，生成 normal-routes-originals072.zip。也可按001、002、003顺序用其他工具作原始字节拼接。

完整ZIP SHA256：036fde3891836a12f25a98f8af1bf3c0fa5974711580ffed72864b82d3c9c33a。它包含16局全部原始记录，144个成员、4,247,404,839字节未压缩文件。16局均已关闭，其中9局达到指定路线，7局提前自然终局；文明平衡未验收。

旧版本存档和录像请保留原程序打开。072构建GUID为3288d9fe24e844748a1183f756874fbf。详细本地ZIP成员清单见package-manifest072.json，当前源码快照8490fc7。此分块只解决传输上限，不改变已验证原件。
