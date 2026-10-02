# 原生事件日志的无损压缩归档

长对局的 JSONL 可能超过单文件发布限制。压缩只改变归档容器，不改变原生报告、事件字节、客户端版本或构建 GUID。本次只准备读取能力和临时控制测试，没有转换、删除或覆盖既有对局档案。

`verify_player.py` 优先读取关联报告同 stem 的 `-events.jsonl`；只有原文件不存在且精确同名的 `-events.jsonl.gz` 存在时才读取 gzip。它复用 `audit_ai_matrix.open_lines`，继续逐行执行原有严格 JSON、身份、支付和完成时间检查。`verify_performance.py` 同样支持精确同名 gzip，仍检查与报告的文件名关联；显式 `--events` 不会改成另一个候选路径。来源 SHA 始终是实际读取的原始或压缩文件的物理字节 SHA，不能把解压 SHA 冒充容器 SHA。性能校验器使用内置 gzip 流读取，保留原先可以独立导入的性质。

归档时先等待原生导出完成，从原文件得到普通审核摘要，并原样保留该摘要，例如 `summary-before-compression.json`。再用 gzip `mtime=0` 和空内嵌文件名压缩已完成的原生导出；原始 NativeExport 留在原位置。新目录只放精确同名的 gzip 时，用同一版本/GUID/对局及合法菜单配置重新审核，保存另一个派生摘要 `summary-gzip-audit.json`。不要覆盖普通审核摘要，也不要把准备工具的控制测试说成新的原生证据。

以下模板只操作传入的已完成源文件和一个新目录。它拒绝覆盖同名容器，并记录容器 SHA、解压后的原始字节数/SHA 与 NativeExport 的比较。实际归档应同时保存报告、菜单及其他配对文件、对应 Rules/DeckLibrary/解析器快照及它们的 SHA；复核时从项目根运行已安装的校验器，深层快照只作为来源证明。

```sh
python3 - '/absolute/path/to/MATCH-match_finished-events.jsonl' '/absolute/path/to/new/archive' <<'PY'
from pathlib import Path
import gzip, hashlib, json, shutil, sys

def identity(path, compressed=False):
    count = 0
    digest = hashlib.sha256()
    opener = gzip.open if compressed else open
    with opener(path, 'rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            count += len(chunk)
            digest.update(chunk)
    return {'bytes': count, 'sha256': digest.hexdigest()}

source = Path(sys.argv[1])
archive = Path(sys.argv[2])
archive.mkdir(parents=True, exist_ok=True)
target = archive / (source.name + '.gz')
before = identity(source)
with source.open('rb') as raw, target.open('xb') as container:
    with gzip.GzipFile(filename='', mode='wb', fileobj=container, mtime=0) as output:
        shutil.copyfileobj(raw, output, 1024 * 1024)
after = identity(source)
decoded = identity(target, compressed=True)
if before != after or decoded != before:
    raise ValueError('Source changed or decompressed bytes do not match NativeExport')
receipt = {'nativeSourcePath': str(source.resolve()), 'nativeSource': before,
           'containerFile': target.name, 'container': identity(target),
           'decompressed': decoded, 'decompressedMatchesNativeExport': True,
           'gzipMtime': 0, 'scope': 'Lossless archive container; runtime identity unchanged'}
with (archive / 'event-compression-receipt.json').open('x', encoding='utf-8') as output:
    json.dump(receipt, output, indent=2)
    output.write('\n')
PY
```

损坏 gzip（截断、CRC 损坏、错误文件头）必须拒绝；即使内容可以解压，重复字段、非有限数和无效实体身份仍由同一严格检查拒绝。控制测试覆盖海岸 III／河盟 V 完整支付与自然结算链的 gzip 等价、完整性能 CLI 的 gzip 等价、精确文件名和损坏拒绝，另保留此前所有检查。测试结果及脚本 SHA 见 `event-archive-verifier-regression.json`。
