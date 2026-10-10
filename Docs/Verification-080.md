# 080 验证记录

## 不可混用的历史尝试

- ImportBaseline1：成功导入，只生成真实HarborRender080的meta；不是源清单不变的测试。
- Baseline1：实际失败；外部runner传入baseline而fixture严格要求baseline-old-0.22。修复外部角色映射，原件保留，不放宽fixture。
- Baseline2：实际1/1通过，但近出生点镜头包含地图外黑边。保留原图；冻结新的中央镜头fixture后另跑Baseline3，未把旧图冒充当前对照。
- Baseline3：实际0.22.0旧模型、802输入、源稳定、1/1通过；它包含新render fixture，不等于未经修改的079发布包。正式对照采用此组。
- ImportCurrent1：应用原创模型、几何fixture及0.23版本后成功导入，实际生成新几何fixture的meta。
- Current1：实际0.23.0、804输入、1/1通过。其后增加Tools/test.sh的源指纹export，故保留为历史画面；最终源另跑Current2。

## 最终源专项与渲染

最终804输入源指纹：`65c82cd863de3fff8d745143301204b1425340aff35de5fd9574ec9b597e555f`。

Focus1：实际36/36通过，27个既有用例及9个新几何用例。原始XML SHA256：`cb2bef1ecd6375f2047c867ae3a10c7bf1360cc1b5b80b2143e001a578b37a33`。码头全部网格边界/4方向/5建造阶段占地；运输船双侧实际三角形开放射线与顶棚正控制；护卫舰两折帆体积/舷炮；5船×220姿态/朝向样本的原始船壳、桥下净空、资源释放均通过。有限样本不表示连续动画全域证明。

Current2：实际1/1通过，最终804输入前后一致。Baseline3与Current2冻结render fixture SHA256均为`a91b346929667007de22c4a7a2202259cefdae41a1e9a810a48db8aa38209fb9`。独立交叉审查115项通过，核对16张原始/重复PNG的文件SHA和解码RGBA SHA；每组重复漂移为0。世界、规则、光照和镜头一致。1712个场景记录完全一致，仅4个不活动的目标码头血条记录Y平移+0.529999733；6个预期模型批次改变、16个其他目标网格不变。新旧画面像素变化是局部模型/阴影变化，不等于画质评分。

## 完整回归

Full1实际退出0、用时1241.9042245秒。EditMode1381/1381、PlayMode483/483，共1864/1864；失败/跳过/不确定0，全部804输入前后完全相同。Tools/verify-delivery.py已对实际当前源复核通过。

原始报告：`Builds/Tests/run-QT839Foi`。
EditMode XML SHA256：`4154b1ecfdeb82f3b4b6afd982accd9b04ea10635b0c472cde71aaaa72e4bebf`。
PlayMode XML SHA256：`2339dd67538bb5176d8a860d231be95a94ee829279ef0b34f80c5b592a7ab5d4`。

Full1启动时HEAD为`5c850b63088baa76ca09c4a5c0efdaf7ca2051b7`，本轮实现尚在工作树；后续提交依据804文件逐字节绑定，旧HEAD本身不包含这些新实现。

## 构建与交付状态

Build1已实际完成并退出0，用时16.471875125秒；构建提交`e43e7dddffccfc3878519219c53435018f200ca1`，前后804源输入与Full1完全一致。实际Info.plist版本0.23.0，严格codesign退出0。app冻结298个文件、191071598字节，逐文件SHA256/Unix mode清单SHA256：`0c842f3b4b43a9f30bbaa5044c486032b1c8ff7f91272afa2ad1d4ba26917557`。这是Development构建，不是公证Release。打包、GitHub与云盘结果后续分别记录，旧079交付身份保持不变。本版尚无原生窗口完整交互/付费港口链、完整人工对局、FPS或两台物理Mac验收；受控渲染不是上述证据。
