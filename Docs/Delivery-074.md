# 0.17.0 私有交付 · 074

2026-10-07。四包、manifest和两sidecar，以及七文件收据/下载说明，均已上传原私有Google Drive；**九文件115988048B已实际读回全部字节，bytes/SHA相同，metadata仅owner可访问**。目录直接子文件数和ID集合也已核。没有共享修改、main合并或公开发行。

运行提交 `ba0b57336c46d72738b71b59af923a149aff467d`，740输入FP `61d00af23e00d960dd040ef55bdfb0aa269b15745be1d1a2f0faec46377d6698`；源码ZIP快照 `e133412599455d1213ff0c20fc6552af6d1bc47a`。Mac arm64 Development/Mono、Unity6000.6.3f1、GUID `1ada3d99ddf84c0f93e1b526e6360390`，实际Succeeded/exit0/sourceStable。正式完整1646/1646（1227Edit+419Play）Passed；范围见[验证](Verification-074.md)、[内容](Content-074.md)、[限制](Known-Issues.md)。

[本版私有目录](https://drive.google.com/drive/folders/1fROjERbIWqKggIDdGN2qfcWb1OrAk0o7)在原Frontier Reckoning项目下。九文件实际权限均shared=false、仅user owner；链接需要原账号权限。

|原件|bytes|SHA256|下载|
|---|---:|---|---|
|source.zip|16330020|`81feafdb99fa62e35167b4bb9dbab40546af7357313658ed6e86f8b95f9c7b16`|[文件](https://drive.google.com/file/d/1shziMs8ZpNmin7XneIcbaA92nZLLQSD8/view?usp=drivesdk)|
|macOS.zip|75955706|`04355af53fa55312eb0580a7657c93699d788c143ecbbe633aa6e1ddd18817dc`|[文件](https://drive.google.com/file/d/1zVKiH1f35q_e3XvuPh8q7uOvlk1etiWc/view?usp=drivesdk)|
|evidence.zip|1371065|`ac0a7b9e10000c0144b3a10db187dcc3db29e2ba40d6cb201f041a1360378948`|[文件](https://drive.google.com/file/d/18rMKvruNfBHzttfty2wGoOwNdWPS0RRw/view?usp=drivesdk)|
|normal.zip|15341789|`279b03cd27081e27d08381c377c416b3c724c24ccfab95b0a8f448a197d7ddec`|[文件](https://drive.google.com/file/d/1QfyPvxyvD4lOO0Ke5vUufSX1HQLRMIjx/view?usp=drivesdk)|
|package-manifest074.json|6558559|`7e907bf48f0ea55c1d7b87d9113ae8c7774b90a4bae61a505563d722cf4f922a`|[文件](https://drive.google.com/file/d/1P6YHIWYjq5V-hClXG865HKAZTnOZ07VZ/view?usp=drivesdk)|
|source-map074.json|94919|`39800cf6ec113a3dfdc4569b8523a210032d5de514dadb76b1a70b1c4ed9786f`|[文件](https://drive.google.com/file/d/1R-28hcoCKpiBE_NiQkhPIJ_5tg_mfRi6/view?usp=drivesdk)|
|actual-app-manifest074.json|318884|`2a58b22bf7aa2117995021074e58af457bbade5a32475ebd93deabe7c77ae8b1`|[文件](https://drive.google.com/file/d/1hbPzNMy9wzdpab0tz005mDGy4YseCbSZ/view?usp=drivesdk)|
|drive074-original-seven-verified.json|12641|`752f8c3fa2e4d122179c605e105aad1aadcf13fe8ee00d930f74c5243de2f679`|[文件](https://drive.google.com/file/d/1CNRd_NSUL2eN4rL4E8q7dBimYQfZmC8p/view?usp=drivesdk)|
|README-074.md|4465|`a6e1ec47aa0cccb49ea9a2011e58861c3c2045c36f54fc61274afa88f00fc569`|[文件](https://drive.google.com/file/d/1EZfdUdG1gn8LnwV32KBzAjV3_SaAPC5e/view?usp=drivesdk)|

源码profile为 `all-tracked-except-Docs-Evidence-074-v1`：1023选中tracked路径/24055448B、4316省略路径/3434608716B，Git path/mode/blob/SHA/byte完整分区保留。包含全部740运行输入，省略Docs/Evidence/**；这是指定源码profile，不能称完整Git历史归档。evidence.zip有53文件（52原件逐字节副本+生成索引README），normal.zip完整29文件，macOS.zip完整298文件。四ZIP成员数source1064/macOS319/evidence70/normal34，各低于100MiB；EOF/CRC/成员集/全部字节SHA/Unix模式和执行flag/Git地址已核。

[实际manifest](Evidence/074/Delivery/package-manifest074.json) SHA `7e907bf48f0ea55c1d7b87d9113ae8c7774b90a4bae61a505563d722cf4f922a`。独立封包复核实际100621项检查、零失败，见[最终-2报告](Evidence/074/Delivery/independent074-package-audit-2.md)；另一次app/normal审查9920项、零失败，不能计入Unity1646用例总数。首审因读取二审不存在的failureCount字段失败，原脚本/报告、临时更正通过和最终新路径复查均保留；没有弱化包或原件检查。root实际helper离线复读exit0，manifest未改。

[最终九文件收据](Evidence/074/Delivery/drive074-final-nine-verified.json)记录真实ID/title/parent/owner/完整byte/SHA。收据证明七个原件、上传的七文件收据和README；最终九文件收据本身只在包外保存，不递归声称已上传自身。ZIP与封包前SourceSnapshot不改；源码ZIP内交付文档是尚待封包/上传的原快照，本页和云盘README记录后续实际结果。最末Git审查以[draft PR1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)当前head为准，避免提交/manifest自引用。

三条普通I/standard双AI、无注资/强制命令，全部达到2400秒cap、未分胜负。真实市场14/15/10笔、集结25/18/20，最后300秒正PvP伤害886/889/642。13657闭合一致性检查零失败，该检查器作者参与Gather实现；不称独立实现审计或六文明平衡验收。

Gather为任务准入与定期复查，standard约1.2秒（easy2.4/hard0.6）；两个审查点间风险、精细恢复和耗尽换目标仍有限制。最大headless Step仍约1.8秒；当前GUID原生交互/冷恢复/录像、FPS、签名公证Release及双物理Mac分别未验收。[073交付](Delivery-073.md)及更早包、截图和验收保持旧身份。
