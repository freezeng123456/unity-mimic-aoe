"""Copy closed originals and document actual 074 results; never run Unity or upload."""
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
OUT = Path('/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.17.0/Candidate1')
report = json.loads((OUT/'Diagnostics/validation074-closed-evidence-2a.json').read_text())
assert report['status'] == 'verified_closed_full_build_declared_three_cases'
assert report['acceptedClosedEvidenceScope'] is True and report['failures'] == []
full = json.loads((OUT/'Full2/runner-receipt074.json').read_text())
tests = Path(full['reportsDirectory'])
evidence = OUT/'EvidenceFinal1'
evidence.mkdir(exist_ok=False)
copies = []
def copy(src, rel):
    dest = evidence/rel
    dest.parent.mkdir(parents=True, exist_ok=True)
    assert not dest.exists()
    shutil.copy2(src, dest)
    raw = src.read_bytes()
    assert dest.read_bytes() == raw
    copies.append({'original':str(src),'relative':rel,'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()})
for src in sorted(tests.iterdir()):
    if src.is_file(): copy(src, 'Tests/Full2/'+src.name)
for name in ('Focused1','Focused2-ProjectionRed','Focused3','Full1','Full2'):
    for src in sorted((OUT/name).iterdir()):
        if src.is_file(): copy(src, 'Tests/'+name+'/Runner/'+src.name)
initial = json.loads((OUT/'Full1/runner-receipt074.json').read_text())
for src in sorted(Path(initial['reportsDirectory']).iterdir()):
    if src.is_file(): copy(src, 'Tests/Full1/OriginalReports/'+src.name)
for name in ('Build1','NormalObserved1'):
    for src in sorted((OUT/name).iterdir()):
        if src.is_file(): copy(src, name+'/'+src.name)
copy(OUT/'NormalObserved1/matrix/manifest.json','NormalObserved1/matrix/manifest.json')
for src in sorted((OUT/'Diagnostics').iterdir()):
    if src.is_file() and src.suffix in ('.json','.md','.py','.diff'):
        copy(src, 'Diagnostics/'+src.name)
(evidence/'README.md').write_text('''# 074 selected closed evidence

Full2/Focussed3 belong to final runtime ba0b573 / 740 inputs / FP61d00. Full1 and Focused1 retain the initial b72 source; ProjectionRed retains the actual one-test failed 5af source. No original is overwritten or relabelled. Focused tests are a subset, not extra tests added to the 1646 full total.

The actual full XML/source-evidence, runner receipts, logs, predeclaration and static/closed-evidence audits are copied byte for byte. Preparation-only helper/checker versions remain preparation records; only validation074-closed-evidence-2a has been executed as the accepted closed-evidence checker. That checker was written by the Gather implementation author and is not an independent implementation audit.

The complete NormalObserved1 tree, including every case's events, samples and boundary states, is carried separately in normal.zip. Only its manifest/runner/logs are selected here. The complete actual app is carried separately in macOS.zip. Packages, cloud delivery, native interaction, cold restore/replay, FPS and broad balance are not certified by the closed-evidence checker.
''')
repo_evidence = ROOT/'Docs/Evidence/074'
assert not repo_evidence.exists()
shutil.copytree(evidence,repo_evidence)
(OUT/'Diagnostics/evidence-copy-index074.json').write_text(json.dumps({'schema':'frontier074-selected-original-copy-index-v1','copies':copies,'originalsByteIdentical':True,'evidenceDirectory':str(evidence),'repoEvidenceDirectory':str(repo_evidence),'scope':'Selected evidence only; complete ordinary tree is separate. Index is external and does not claim its own membership.'},indent=2)+'\n')

cases = report['caseReports']
natural = sum(c['declaredIdentity']['status']=='natural_terminal' for c in cases)
caps = len(cases)-natural
outcome = f'{len(cases)}条普通I观察已关闭：{natural}自然终局、{caps}达到2400秒上限'
table = ['|对局|实际结果/模拟秒|真实Gather事件 P0/P1|市场/集结|拒绝采样行/累计数|末300秒正PvP伤害|', '|---|---|---:|---:|---:|---:|']
for c in cases:
    d=c['declaredIdentity']; g=c['gather']; a=c['army']
    table.append(f"|{c['id']}|{d['status']} / {d['time']:.6f}|{'/'.join(str(x['eventCount']) for x in g['actualCredits'])}|{c['market']['count']}/{a['assembledCount']}|{g['sampleRowsWithRejections']}/{g['sumRejectedAcrossSampleRows']}|{a['last300Seconds']['positivePvpDamageCount']}|")
verification = f'''# 074 验证记录

2026-10-07。运行源码 `ba0b57336c46d72738b71b59af923a149aff467d`，740输入，FP `61d00af23e00d960dd040ef55bdfb0aa269b15745be1d1a2f0faec46377d6698`。

最终完整 `run-xijamd2m`：1227 EditMode + 419 PlayMode，**1646/1646 Passed**，零失败/跳过/未决，实际exit0且前后源码稳定。073旧1611 fullname保留，新增18 Gather和17 Home；Focused3的64项是子集，不叠加到完整总数。完整原件见[Full2源码证据](Evidence/074/Tests/Full2/source-evidence.json)。

最初b72源码Focused1 63/Full1 1645通过，后续投影距离用例在5af源码实际0/1失败：普通Gather可走到投射工作点，但边缘距离2.6大于实际采集范围1.5，16秒真实运动无木材收入。最终准入guard修正后，同一测试文件/断言未弱化，Focused3 64通过，包括清障后的真实正收入。历史原件和各自指纹分别保留。

实际Mac Build1：0.17.0，Unity6000.6.3f1，arm64 Development/Mono，Succeeded/exit0，源码前后稳定，GUID `{report['buildGuid']}`。未调用Prepare。当前GUID原生交互、冷恢复、录像、FPS和发布签名未验收。

{outcome}。预声明 winding Ottoman44/45、river_bend Dutch42；I时代、上限V、standard、双AI、2400秒cap、1500比较点、30秒采样。没有运行注资、强制命令或干预。

{chr(10).join(table)}

真实Gather事件与每tick float32 gathered账本分别核对；缓冲日志的double总和不冒称与账本精确相等。市场按每笔报价、实际扣费/到账、样本和边界账本核对；Post/Ledger、运输、集结、伤害和自然终局/cap按原事件核对，零触发原样保留。完整闭合证据检查实际{report['checks']}项、零失败，见[报告](Evidence/074/Diagnostics/validation074-closed-evidence-2a.md)。验证者编写了Gather实现，不宣称独立实现审计。

拒绝采样数包含隐藏/失效/耗尽目标、资格、物理不通、工作点超距离和普通路径危险；它不是独立工人数、穿越敌区次数或完整轨迹。约1.2秒AI审查仍允许两个审查点之间发生新风险。headless Step/观察/写盘计时不认证GUI帧率、纯Home性能因果或六文明平衡。

选定原件在Evidence/074，完整三局原件另在normal.zip；封包与云盘结果见[交付记录](Delivery-074.md)。073的1611项、旧GUID市场建造/100W付款/完成及原生兑换未完成保持旧身份，见[073验证](Verification-073.md)。
'''
(ROOT/'Docs/Verification-074.md').write_text(verification)
replacements = {
    'Docs/Completion-Status.md': [('Focused3实际64/64通过、exit0、源码稳定；最终Full2正在运行，构建与三场普通观察尚未执行完毕。',f'Focused3实际64/64通过；最终Full2实际1646/1646通过，exit0、源码稳定；Build1实际Succeeded/exit0、GUID {report["buildGuid"]}。{outcome}。')],
    'Docs/Known-Issues.md': [('Focused3实际64/64通过，Full2仍在执行。',f'Focused3实际64/64、Full2实际1646/1646通过，Build1实际Succeeded/exit0；{outcome}。')],
    'Docs/AoE3-Gap-Roadmap.md': [('最终Full2正在运行，构建和预声明三条普通观察仍待实际关闭。',f'最终Full2实际1646/1646通过，Build1实际Succeeded/exit0；{outcome}，未把cap改为获胜。')],
    'README.md': [('三条预声明普通I观察正在执行。',outcome+'。'), ('三条预声明普通开局仍在执行。',outcome+'。')]
}
for name, rows in replacements.items():
    p=ROOT/name; text=p.read_text()
    for old,new in rows:
        assert old in text,(name,old)
        text=text.replace(old,new)
    p.write_text(text)
(ROOT/'Docs/Delivery-074.md').write_text(f'''# 0.17.0 私有交付 · 074

2026-10-07。最终1646项回归、Mac Build1和三条普通开局观察已闭合；{outcome}。运行源码ba0b573/740输入/FP61d00，构建GUID `{report['buildGuid']}`。范围见[验证](Verification-074.md)与[限制](Known-Issues.md)。

这是封包前文档快照。当前尚未创建四ZIP或上传文件；候选目录已在[原私有Google Drive项目](https://drive.google.com/drive/folders/1fROjERbIWqKggIDdGN2qfcWb1OrAk0o7)下创建，metadata已读回shared=false、仅owner。后续包外终态文档/云盘README记录实际SourceSnapshot、ZIP字节/SHA及远端完整读回；本页不虚构自己的提交或包manifest哈希。

计划源码profile包含全部tracked路径但省略Docs/Evidence/**；选定证据另在evidence.zip，完整普通局另在normal.zip，完整实际app另在macOS.zip。该源码profile不是完整Git历史归档。原生交互、冷恢复/录像、FPS、广泛平衡和签名发布仍未验收。

既有[私有draft PR1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)保持原审查入口；不合并main、不公开发行、不扩大共享。073包及[原交付](Delivery-073.md)保持原字节与身份。
''')
print(json.dumps({'status':'copied_closed_evidence_and_finalized_prepackage_docs','originalCopies':len(copies),'checks':report['checks'],'naturalTerminal':natural,'observationCap':caps}))
