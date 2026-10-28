from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil,gzip,base64,struct
root=Path('/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.9.0/FinalCandidate')
project=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
data=Path('/Users/Apple_501/Library/Application Support/Frontier Workshop/Frontier Reckoning')
dest=project/'Docs/Evidence/066/Native'; gallery=root/'Gallery'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
load=lambda p:json.loads(p.read_text())
write=lambda p,j:p.write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n')
before=load(root/'Native/storage-before.json');receipt=load(dest/'client-build.json')
old=set(before['capturedBefore']); captures=sorted([p for p in (data/'Captures').glob('*.json') if p.name not in old],key=lambda p:load(p)['utc'])
assert len(captures)==20,len(captures)
labels=['初始主菜单','通过GUI保存放大界面','冷恢复音量','冷恢复无障碍设置','冷恢复Q改键','七课教学库','课程选取阶段','课程槽3保存后暂停','新进程冷恢复课程暂停','恢复后的移动目标阶段','真实到达后课程完成','新进程课程完成徽记','原创战役与章节锁','2对2队伍配置','曲河2对2运行','Q改键攻击移动命令接受','新进程恢复四人存档','玩家投降后盟友继续比赛','四方自由混战配置','四国自由混战实际开局']
(dest/'Captures').mkdir(parents=True,exist_ok=True);gallery.mkdir(exist_ok=True)
index={'schema':1,'appVersion':'0.9.0','nativeBuildGuid':receipt['buildGuid'],'runtimeCommit':receipt['runtimeCommit'],'sourceFingerprint':receipt['sourceFingerprint'],'sourceFiles':receipt['sourceFileCount'],'captures':[]}
for meta,label in zip(captures,labels):
 j=load(meta);png=meta.with_suffix('.png')
 assert j['appVersion']=='0.9.0' and j['buildGuid']==receipt['buildGuid'] and j['schemaVersion']==3
 assert j['pngFile']==png.name and j['pngSha256']==sha(png)
 assert struct.unpack('>II',png.read_bytes()[16:24])==(j['width'],j['height'])
 assert j['matchId']==j['requestMatchId'] and j['requestFrame']<=j['captureFrame']
 for folder in (dest/'Captures',gallery):
  for p in (png,meta): shutil.copy2(p,folder/p.name)
 index['captures'].append({'label':label,'png':png.name,'metadata':meta.name,'pngSha256':sha(png),'metadataSha256':sha(meta),'matchId':j['matchId'],'worldTime':j['worldTime'],'inMenu':j['inMenu'],'paused':j['paused'],'players':j['players'],'guidedId':j['guidedId'],'guidedStatus':j['guidedStatus'],'guidedStage':j['guidedStage']})
write(dest/'Captures/capture-index.json',index);write(gallery/'capture-index.json',index)
def decode(name):
 e=load(root/'Native'/name);raw=gzip.decompress(base64.b64decode(e['payloadBase64']));assert hashlib.sha256(raw).hexdigest()==e['sha256'];return json.loads(raw)
guided=decode('guided-checkpoint-slot3.json');teams=decode('teams-checkpoint-slot3.json');prefs=load(root/'Native/preferences-cold-restored.json')['settings'];account=load(root/'Native/account-after-native-completion.json');assert hashlib.sha256(account['payload'].encode()).hexdigest()==account['sha256'];record=json.loads(account['payload'])['records'][0]
assert guided['hasGuided'] and guided['guided']['stageIndex']==1 and guided['guided']['attemptId']==guided['matchId']
assert [p['state']['team'] for p in teams['world']['players']]==[0,1,0,1]
assert sorted(p['player'] for p in teams['simulation']['controllers'])==[1,2,3]
assert prefs['friendEnemyPatterns'] and prefs['highContrastMinimap'] and next(x['key'] for x in prefs['hotkeys'] if x['action']==1)==31
assert record['id']=='lesson_selection' and record['completions']==1 and record['lastAttemptId']==guided['matchId']
assert index['captures'][8]['matchId']==guided['matchId'] and index['captures'][8]['worldTime']==guided['world']['time'] and index['captures'][8]['guidedStage']=='抵达标记'
assert index['captures'][10]['guidedStatus']=='Completed' and index['captures'][10]['worldTime']==record['bestSeconds']
assert index['captures'][16]['matchId']==teams['matchId'] and index['captures'][16]['worldTime']==teams['world']['time']
assert index['captures'][17]['players'][0]['resigned'] and not index['captures'][17]['paused'] and not any(p['winner'] for p in index['captures'][17]['players'])
assert [p['team'] for p in index['captures'][19]['players']]==[0,1,2,3]
command=[e for e in teams['world']['events'] if e['kind']=='Command' and e['player']==0 and e['message'].startswith('AttackMove')]
assert command
for n in ['guided-checkpoint-slot3.json','teams-checkpoint-slot3.json','preferences-cold-restored.json','account-after-native-completion.json']:shutil.copy2(root/'Native'/n,dest/n)
# Restore only files created by this acceptance run, with exact owned hash checks.
owned={'Saves/slot-3.json':'teams-checkpoint-slot3.json','Preferences/player-settings.json':'preferences-cold-restored.json','Account/guided-progress.json':'account-after-native-completion.json'}
restoration=[]
for item in before['files']:
 p=data/item['relativePath'];out={'relativePath':item['relativePath'],'originalExisted':item['existed']}
 if item['existed']:
  assert p.is_file() and sha(p)==item['sha256'];out.update(originalSha256=item['sha256'],currentSha256=sha(p),unchanged=True)
 else:
  snapshot=root/'Native'/owned[item['relativePath']];assert p.is_file() and sha(p)==sha(snapshot);out.update(ownedSha256=sha(snapshot),restoredAbsence=True);p.unlink();assert not p.exists()
 restoration.append(out)
write(dest/'storage-restoration.json',{'recordedUtc':datetime.now(timezone.utc).isoformat(),'passed':True,'scope':'Original slots 1/2 unchanged. Only exact-hash files created by this native acceptance were removed to restore original absence. Unique capture pairs retained.','files':restoration})
report={'schema':1,'appVersion':'0.9.0','buildGuid':receipt['buildGuid'],'runtimeCommit':receipt['runtimeCommit'],'sourceFingerprint':receipt['sourceFingerprint'],'sourceFiles':receipt['sourceFileCount'],'recordedUtc':datetime.now(timezone.utc).isoformat(),'passed':True,'nativeCapturePairs':20,'checks':{'settingsColdRestart':{'passed':True,'masterVolume':prefs['masterVolume'],'uiScale':prefs['uiScale'],'friendEnemyPatterns':True,'highContrastMinimap':True,'attackMoveBinding':'Q','method':'GUI changes in session-01; readback screenshots from session-02; filesystem snapshot unchanged in later cold processes'},'guidedColdRestoreAndActualCompletion':{'passed':True,'lesson':'lesson_selection','attemptId':guided['matchId'],'checkpointSeconds':guided['world']['time'],'checkpointStage':guided['guided']['stageIndex'],'completedSeconds':record['bestSeconds'],'completionCount':1,'method':'GUI select, Save Slot3, terminate old processes, new session-04 Load3, Resume, right click objective, actual arrival and automatic terminal pause; session-05 completed badge'},'teamsColdRestore':{'passed':True,'matchId':teams['matchId'],'checkpointSeconds':teams['world']['time'],'teams':[0,1,0,1],'aiSlots':[1,2,3],'trainedBySlot':[p['state']['trained'] for p in teams['world']['players']],'method':'session-05 GUI 2v2 start/save; all candidate processes absent before session-06 GUI Load3; same matchId/worldTime/roster restored paused'},'reboundAttackMove':{'passed':True,'key':'Q','commandEvidence':command,'method':'Actual native GUI Q then left-click; screenshot shows AttackMove order accepted and moving; checkpoint contains player0 accepted Command'},'teamMemberResign':{'passed':True,'method':'session-06 GUI Resign; player0 resigned, ally alive, world time advances and no terminal winner'},'fourCivilizationFFA':{'passed':True,'teams':[0,1,2,3],'civilizations':['coast','river','france','ottoman'],'method':'session-06 GUI roster and native start; F9 actual four players'},'cjkRendering':{'passed':True,'method':'Visual native inspection of library, objective and terminal Chinese text'},'singleSharedSightMinimap':{'passed':True,'method':'Independent agent inspected actual native PNG and source; one 128x128 texture with two allied explored regions'},'protectedStorageRestored':{'passed':True,'receipt':'storage-restoration.json'}},'processes':[{'log':f'session-{i:02}.log','exitCode':143 if i in (2,3) else 0} for i in range(1,7)],'diagnosticProcessNote':'During exit inspection get_app_state transparently relaunched one unlogged candidate process; it was explicitly terminated, and was not used for any passing cold-restore proof. Sessions 02/03 were both terminated before session04. Sessions05/06 started after all candidate processes were absent.','scope':'Native checks cover selected representative flows, not native completion of all 10 guided scenarios, four-player match victory, 400-unit performance, network/replay, signing/notarization, or Intel macOS. Automated scenario/relationship coverage is in Tests. Unity Development shutdown warnings are preserved in logs.'}
write(dest/'native-acceptance.json',report);write(root/'Native/native-acceptance.json',report)
readme='# 0.9.0 原生验收\n\n构建 GUID `'+receipt['buildGuid']+'`，源码指纹 `'+receipt['sourceFingerprint']+'`，573 输入；1062 完整测试另见 `../Tests/README.md`。本目录保留20组未修改的 F9 PNG/JSON、实际教学与团队存档、设置/账号快照和受控文件恢复回执。\n\n'+report['scope']+'\n\n'+report['diagnosticProcessNote']+'\n\n|截图|实际画面|\n|---|---|\n'+''.join(f"|[{x['png']}](Captures/{x['png']})|{x['label']}|\n" for x in index['captures'])+'\n原生通过结论以 `native-acceptance.json` 中具体范围为准；各日志原文/脱敏哈希另见 `log-redaction-receipt.json`。\n'
(dest/'README.md').write_text(readme)
(gallery/'README-zh.md').write_text('# Frontier Reckoning 0.9.0 原生截图\n\n20组来自同一 macOS arm64 Development 构建的真实 F9 PNG及同名JSON；没有拼接或渲染替代。每组身份、分辨率、PNG哈希和帧顺序可核验。\n\n验证了设置冷恢复、中文课程及槽3恢复后真实到达目标、完成徽记冷恢复、曲河2对2运行与冷恢复、Q改键、投降后盟友继续，以及四国自由混战实际开局。自动化1062项测试另见源码包。仅覆盖这些原生操作，不代表全部战役原生通关、完整四人胜局或联网。\n\n|截图|实际画面|\n|---|---|\n'+''.join(f"|[{x['png']}]({x['png']})|{x['label']}|\n" for x in index['captures']))
client=root/'ClientPayload/Frontier-Reckoning-0.9.0';client.mkdir(parents=True,exist_ok=True)
(client/'README-zh.md').write_text('# Frontier Reckoning 0.9.0\n\n独立原创即时战略游戏。将本目录完整解压到本地，启动 `Frontier Reckoning.app`。适用 Apple Silicon macOS；此包是 Unity 6000.6.3f1 Development/Mono 构建，未签名公证，未验证 Intel Mac。\n\n本批新增：本地2对2与4方混战（你控制slot1，其余AI）、共享盟友视野和团队胜负；七课中文操作教学；三章原创河谷战役逐章解锁；独立课程进度；音效/音乐/环境音分项音量、界面缩放、高对比小地图、友敌图案、事件文字与20项按键改绑。\n\n开始：可先进入 GUIDED LESSONS 的「选取与移动」，单击己方探路者，右键金色目标并实际抵达。完整玩家指南在源码包 `Docs/Player-Guide-066.md`。菜单 ROSTER 顶部按钮循环1v1、2v2、4方混战；MAP按钮选择九张地图。你拥有独立经济与控制权，盟友共享视野。\n\n默认右键移动/攻击目标；A攻击移动、S停止、J固守、G驻军、R修复、H家乡、B建造、V或句点空闲工人。SETTINGS→HOTKEYS可改绑；本测试曾改为Q，但交付不含测试偏好或个人存档，初次启动使用默认值。Escape暂停，F8性能采样，F9真实PNG/JSON截图；保留F8-F10、数字和修饰键。\n\n通过GUI保存到槽1–3；存档严格匹配版本。现有0.7/0.8存档保持原文件，0.9会提示版本不兼容。设置、账号完成记录与对局存档独立保存在 macOS Application Support/Frontier Workshop/Frontier Reckoning 下。测试使用的槽3、设置和账号记录已受控恢复为原状态。\n\n完整回归1062/1062（710 EditMode+352 PlayMode），源码指纹 '+receipt['sourceFingerprint']+'，GUID '+receipt['buildGuid']+'。另有真实原生代表性操作验收和截图；这不证明所有课程原生通关或新的400单位性能指标。\n\n包含六文明、五时代、家乡卡组、曲河/支流/河口、港区/水战、贸易与探索等已有机制。尚未提供联网/回放/关卡编辑器、官方全部国家和战役、角色配音；建模和战役规模仍需扩展。资产许可见 Licenses；Noto CJK字体按OFL分发。\n')
print('Native assembly PASS:',len(captures),'capture pairs; protected storage restored')
