# 素材来源与许可

本轮核对日期：2026-10-05，适用 064 / 0.7.0 候选。新增工业重炮、工厂马车与砖砌工厂均由既有原创几何代码扩展；没有新增下载模型、商业游戏素材、外部纹理、动画或音频。既有 Noto 字体、原创火枪手 FBX / Blender 源文件、Unity 依赖及随附许可继续沿用。下文带版本号的记录保留各自历史身份，不能作为 0.7.0 的美术或原生运行验收结果。当前范围见 [Classic-Industry-064](Classic-Industry-064.md) 与 [Known-Issues](Known-Issues.md)。

## 0.7.0 / 064 原创工业资产增量

| 新增或调整内容 | 可审查来源 | 来源与许可边界 |
| --- | --- | --- |
| Heavy Cannon、Rocket、Great Bombard 的车架、车轮、炮管或火箭导轨 | [ProceduralArt.cs](../Assets/Frontier/Presentation/ProceduralArt.cs) 的 `IndustrialArtillery`，沿用 [DetailedUnits.cs](../Assets/Frontier/Presentation/DetailedUnits.cs) 的合批与共享材质路径 | 本项目编写的几何、枢轴与颜色；没有下载炮械模型、商业纹理、第三方骨骼或动画片段。 |
| 工厂马车、机械货箱、飞轮货物、牵引杆与队色牌 | [ProceduralArt.cs](../Assets/Frontier/Presentation/ProceduralArt.cs) 的 `FactoryWagon` | 沿用原创基础几何与车辆表现；模型不是外部素材包或商业游戏资源的提取、改色。 |
| 砖砌工厂、锯齿屋顶、烟囱与外置飞轮 | [DetailedArchitecture.cs](../Assets/Frontier/Presentation/DetailedArchitecture.cs) 的 `Factory`，沿用 ArchitecturalGeometry / ArchitecturalMaterials | 本项目生成的建筑网格与程序材质，没有新增照片、扫描或下载贴图。 |
| 工业动作、存读档、AI 与呈现优化代码 | UnitRig、UnitAnimation、WorldView、RtsInput 与 Simulation 下的本项目 C# | 原创运行逻辑与现有资产处理，不引入新外部依赖或另行授权的美术输入。 |

本候选没有替换字体、FBX、Blender 源文件或 Package Manager 依赖；这些资产的来源、历史哈希记录、许可全文与第三方声明保持原有适用范围。本次不把历史哈希核对日期改写为新日期，也不将原创几何来源说明扩大为对 Unity 或其他依赖的额外授权。

工厂与存档功能已实现，最终冷启动原生存读档、工业视觉/操作及本版性能验收仍待完成。本节仅说明资产来源，不宣称新的正常对局、测试数量或原生 FPS 已通过。

## 0.4.0 / 060 原创视觉资产与程序材质

以下保留 2026-10-04 的 060 / 0.4.0 来源记录。新增建筑、单位细节、资源、地面和程序材质均由本项目代码制作；未导入《帝国时代》商业模型、纹理、源码、卡牌画、音频或界面资源。对应视觉改造说明见 [Model-Upgrade-060](Model-Upgrade-060.md)。

| 新增或调整内容 | 可审查来源 | 来源与许可边界 |
| --- | --- | --- |
| 14 类建筑、六文明样式、时代附件与施工分组 | [DetailedArchitecture.cs](../Assets/Frontier/Presentation/DetailedArchitecture.cs)、[ArchitecturalGeometry.cs](../Assets/Frontier/Presentation/ArchitecturalGeometry.cs)、[ArchitectureModel.cs](../Assets/Frontier/Presentation/ArchitectureModel.cs) | 本项目编写的顶点/三角形、截面旋转体、屋面、门窗、拱廊和合并网格；不是导入、提取或重新贴色的商业游戏模型。专属建筑按其文明归一化，组合测试数量不表示同数目的独立原作资产。 |
| 建筑砖石、灰泥、瓦/石板、木材、金属、玻璃、布和土地表面 | [ArchitecturalMaterials.cs](../Assets/Frontier/Presentation/ArchitecturalMaterials.cs) | 原创周期函数、砖缝/纹理规则与法线计算生成 256px 颜色/法线贴图，配合 URP 参数；没有外部照片、扫描、商业纹理或生成式位图输入。材质算法的原创来源不替代 Unity/URP 使用许可。 |
| 人物、坐骑、炮械和货车细节及颜色图集 | [DetailedUnits.cs](../Assets/Frontier/Presentation/DetailedUnits.cs)、既有 [ProceduralArt.cs](../Assets/Frontier/Presentation/ProceduralArt.cs)、[AuthoredInfantry.cs](../Assets/Frontier/Presentation/AuthoredInfantry.cs) | 原创衣身、面部、装备、马具、炮管和车辆网格，按关节/显隐组合并；共享颜色图集由代码生成。继续使用 0.2.4 的原创火枪手 FBX，不引入第三方骨骼或动画片段。 |
| 树木、矿石、浆果、鹿、货箱与环境道具 | [DetailedEnvironment.cs](../Assets/Frontier/Presentation/DetailedEnvironment.cs) | 本项目手工编写的分叉、截面、枝叶、矿脉和分类货物几何；未使用下载植物包、动物模型或扫描资产。 |
| 草地/裸土表面、中立贸易车 | [LandscapePresentation.cs](../Assets/Frontier/Presentation/LandscapePresentation.cs)、[WorldView.cs](../Assets/Frontier/Presentation/WorldView.cs) | 原创噪声与纹理函数生成 1024px 地面颜色/法线图，中立车辆使用原创合并几何；不复制外部地图、地表图或车辆模型。 |
| URP 建筑、时代、施工、单位与环境检查图 | ArchitectureReview060Tests、DetailedModelStudy060Tests 与各图来源回执 | 图像来自项目自己的网格、材质和 URP 实际渲染；文字沿用既有 Noto 字体及 OFL。检查图不是参考图、概念图，也不单独证明普通对局、人工操作或渲染性能。 |

《帝国时代 III：决定版》官方美术访谈与截图只作为外部观察参考，来源和具体观察见 [Architecture-References-060](Architecture-References-060.md)。官方图片不进入运行时资产或作为本项目原创图像分发；六文明建筑语言是本项目的设计方向，不宣称获得商业资产授权、逐个复刻原作国家模型或得到原作方认可。

2026-10-04 重新计算，`NotoSans-Regular.ttf`、`FrontierInfantry.fbx`、`SourceAssets/Characters/Infantry024/FrontierInfantry.blend` 的文件大小与 SHA-256 均与下文原记录一致。FBX 本轮只调整 Unity importer 的可读设置以支持运行时合批，未替换 FBX 文件。实际锁定的 URP 17.6.0、Input System 1.20.0、UGUI 2.6.0、Test Framework 1.8.0 继续沿用；随附许可全文、字体归属与第三方声明不变。

本轮主方向光启用软阴影，仍使用现有 URP；这项表现设置不引入新的外部资产，也不改变引擎或依赖许可。最终原生构建、性能与图审状态由 060 验收记录单独说明。


## 本项目制作（055 历史清单）

| 内容 | 来源 | 用途与范围 | 验收边界 |
|---|---|---|---|
| 人物/骑兵/火炮/建筑/资源/地形/商队/宝藏模型 | `Assets/Frontier/Presentation/ProceduralArt.cs`、`OutcropArt.cs` 与 `WorldView.cs` 的原创几何，火枪手保留本项目制作的FBX | 本项目原创资产；没有外部商业模型输入 | 055新增统一低多边形轮廓；常规距离原生视觉与阶段D验收待验证 |
| UI、菜单、图形、文案 | 本项目 C# 原生 UGUI 与 LineRenderer | 独立名称、布局、视觉 | 已做部分原生可读性复查，完整本地化未完成 |
| 枪/炮/近战/命中/单位活动/分类提示音/环境/音乐 | `EventAudio.cs`、`ActivitySoundClips.cs`、`NoticeSoundClips.cs`、`AmbientAudio.cs` 中的原创正弦/噪声、频率序列与包络合成 | 无外部录音、采样乐器、商业游戏音频或第三方乐谱输入 | 已有声音实现；055原生监听器混音与正常玩家耳听验收待验证 |

| 命中尘土、开火闪光、死亡消散 | `CombatFeedback.cs` 原创程序化几何与共享URP材质 | 固定上限的开发反馈效果 | 自动验证生命周期，未完成原生视觉验收 |

## 字体

使用未修改的 **Noto Sans Regular**，由 [Noto 官方仓库](https://github.com/notofonts/noto-fonts/blob/main/hinted/ttf/NotoSans/NotoSans-Regular.ttf)取得，作者和版权声明保留在原字体与许可文件。SIL OFL 1.1 的完整原文随工程附在 `Licenses/Noto-OFL.txt`；[官方使用说明](https://notofonts.github.io/noto-docs/website/use/)支持在商业及非商业产品中使用，须保留许可，不单独销售字体。没有修改字体或使用修改后的保留名称。

实际文件 `Assets/Resources/Fonts/NotoSans-Regular.ttf`，569208 bytes，SHA-256 `b85c38ecea8a7cfb39c24e395a4007474fa5a4fc864f6ee33309eb4948d232d5`。UI 显式读取此资产，不依赖系统字体。早期 LegacyRuntime.ttf 已从运行时代码移除。

## Unity 依赖及第三方声明

这些是实际 Package Manager 导入的版本。当前 manifest/lock 统一：URP 17.6.0、Input System 1.20.0、UGUI 2.6.0、Test Framework 1.8.0。UGUI 早期请求 2.0.0 已更正，不能把其当作实际交付版本。

以下许可与第三方声明直接从实际包复制，源文件哈希和相对路径保存在 `Licenses/package-license-index.json`。这些文件随源码和客户端分包保留；不依赖被排除的 Library/PackageCache。官方来源为 Unity Package Manager（packages.unity.com）或本编辑器内置包；[Unity 官方包文档](https://docs.unity3d.com/Manual/PackagesList.html)仅为索引，使用范围由随附全文决定。

| 包 | 实际版本 | 随附文件目录 |
|---|---|---|
| `com.unity.collections` | 6.6.0 | `Licenses/com.unity.collections/` |
| `com.unity.ext.nunit` | 2.1.0 | `Licenses/com.unity.ext.nunit/` |
| `com.unity.inputsystem` | 1.20.0 | `Licenses/com.unity.inputsystem/` |
| `com.unity.nuget.mono-cecil` | 1.11.6 | `Licenses/com.unity.nuget.mono-cecil/` |
| `com.unity.profiling.core` | 1.0.3 | `Licenses/com.unity.profiling.core/` |
| `com.unity.render-pipelines.core` | 17.6.0 | `Licenses/com.unity.render-pipelines.core/` |
| `com.unity.render-pipelines.universal-config` | 17.6.0 | `Licenses/com.unity.render-pipelines.universal-config/` |
| `com.unity.render-pipelines.universal` | 17.6.0 | `Licenses/com.unity.render-pipelines.universal/` |
| `com.unity.searcher` | 4.9.5 | `Licenses/com.unity.searcher/` |
| `com.unity.shadergraph` | 17.6.0 | `Licenses/com.unity.shadergraph/` |
| `com.unity.test-framework.performance` | 6.6.0 | `Licenses/com.unity.test-framework.performance/` |
| `com.unity.test-framework` | 1.8.0 | `Licenses/com.unity.test-framework/` |
| `com.unity.ugui` | 2.6.0 | `Licenses/com.unity.ugui/` |

Unity Companion License 或各包许可允许的范围以 Unity 项目及构建用途为依据，不能推断所有包都可脱离 Unity 任意复用。各 Third Party Notices 中的组件继续遵循其原许可证。Test Framework、NUnit 和测试性能工具是开发验证依赖，不作为游戏玩法资产。内置引擎模块遵循使用者的 Unity 编辑器/运行时许可；本项目不重新分发 Unity 编辑器或 PackageCache。

后续任何外部模型、贴图、动画、音频进入工程前均需增加作者、原始下载页、版本/哈希、许可全文、归属和可再分发范围。公开可下载的仓库本身不作为授权证据。

## 0.2.3 原创增量

ProceduralArt.cs 新增弧形弓网格、两段弦、军刀/护手、枪托/枪管、长枪头；UnitRig/UnitAnimation 的刚性关节及姿态由本项目编写，未引入第三方模型、贴图或动画。Tests/PlayMode/BaselineArt022.cs 是本项目 0.2.2 源码包中的 ProceduralArt.cs 测试副本，仅改类名/命名空间及来源注释，不进入客户端。Unity 离屏诊断图使用这些原创网格和既有 Noto Sans 字体，字体继续遵守随附 OFL。它们属于开发检查资产，不是最终美术承诺。

## 0.2.4 原创人物源文件

火枪步兵身体由本项目用 Blender 5.2.1 LTS 编写的截面网格、衣片与刚性枢轴制作。无外部模型、贴图、扫描、AI 生成图像或商业游戏输入。可编辑 `.blend`、可重复生成的 `build_character.py` 与 `metrics.json` 位于 `SourceAssets/Characters/Infantry024/`；客户端只导入 `Assets/Resources/Models/FrontierInfantry.fbx`，不依赖 Blender 安装。原材质为十种原创纯色材质，Unity 中替换为项目共享 URP 材质。帽子、头部、衣片、手掌和靴属于原创开发资产，刚性接缝、骑乘和其余兵种未完成最终美术。

源网格统计 3445 三角形、1827 顶点、36 个对象（含空枢轴），约1.895米高，不包含运行时加上的旧武器。不是400单位整场渲染开销的预测。Tests/PlayMode/BaselineArt023.cs 来自本项目冻结0.2.3源码，仅供离屏对比，不进入客户端。Blender编辑器本身不随包再分发。

FBX: 125116 bytes，SHA-256 `6dd0e77f43bfd119283249d68733c8ec9efb120353633ebcf6f408851a69b396`。

Blend: 138446 bytes，SHA-256 `4e89e50467992dc8fa9ccf61700ce8134c732c255072561e48bbba610428d039`。

## 0.2.7 输入与验证增量

本轮只增加原创C#输入/模型选择逻辑、测试和说明，没有引入外部模型、贴图、音效、动画、卡牌插画或字体。包围盒读取当前已有原创模型；字体、FBX、Blender源及Package Manager依赖继续沿用前述许可。无商业游戏素材导入。

## 0.2.14 / 035 记录增量

本轮新增原创C#经济事件、截图状态、测试与验证文档，不引入外部素材或修改运行时字体/FBX。已重新核对Noto字体与FrontierInfantry.fbx的SHA-256，仍与上表一致。Dependencies保持既有锁定版本。完整许可和第三方声明同时随源码Docs/Licenses及独立客户端Docs/Licenses分发。

## 0.2.18–0.2.19 / 041–042 增量

本轮只增加本项目原创 C# 规则/镜头输入/诊断、测试与文字说明。没有新增外部素材，没有更换字体、FBX、Blender源和依赖版本；原始许可范围与随附全文保持适用。

0.2.20仅调整原创项目构建/场景的阴影设置，没有新增或修改外部素材，依赖版本未变。

## 0.2.21–0.2.22 / 045–046 增量

本轮只有原创C#、测试、HUD文字与文档改动。没有新增或替换外部素材、字体、FBX、Blender源或依赖；沿用以上来源、许可全文及既有适用范围。实时阴影保持关闭。

## 0.2.23 / 047 增量

仅原创AI C#、测试与文档，无新增/替换外部素材、依赖、字体、模型或Blender源；沿用既有许可清单与全文。构建输入对照确认素材/依赖未改。

## 0.2.24 / 048 增量

仅新增本项目AI搜索策略、Editor观察器、测试与文档。无新增外部素材、字体、模型、依赖或Blender资产；沿用既有许可清单与全文。素材、规则、场景及依赖构建输入与0.2.23一致。

## 0.2.25 / 049 增量

新增本项目原创ActivitySoundClips C#合成算法与七种由其生成的短PCM16 WAV验证样本，无外部录音或商业游戏素材；代码与参数见Audio-Design-049.md和Assets/Frontier/Presentation/ActivitySoundClips.cs。样本可通过Editor/AudioAudit再生，客户端直接运行相同合成模块。无新增依赖、模型、字体或图片。音色是开发反馈，未宣称最终录音品质或耳听验收。

## 0.2.26 / 050 增量

新增WorkerEquipment原创C#程序化斧、镐、锤、篮筐网格与UnitAnimation工作手势。仅使用Unity原生几何/材质和本项目代码，无外部模型、录音、图像或商业游戏资源。Evidence/050图片由相同项目原生URP离屏渲染生成，用于开发对照；人物其余造型与既有许可清单保持原样。没有新依赖或需另行授权的输入。

## 0.2.27 / 051 增量

仅修改项目原创C#指令目标、临时友军避障和测试；无新外部模型、图像、声音或字体，无新依赖。实时阴影持续关闭，既有素材许可原样保留。

## 0.2.28 / 052增量

仅项目原创C#战斗指令/自动选敌和测试调整，没有新增模型、图像、音频、字体或外部依赖。既有许可全文保留，实时阴影持续关闭。

## 0.2.29–0.2.30 / 053–054 增量

这些历史迭代增加或修复本项目原创C#运行、输入、HUD、测试和证据，没有更换字体、FBX或Blender源，没有新增第三方美术/音频输入。6000.6.3f1环境中Searcher间接包为4.9.5，直接依赖仍按前表；随附原许可证和第三方声明继续独立适用。0.2.30正常玩家截图、原始事件与诊断报告是本项目原生运行结果，不是本轮055视觉/音频验收结果。

## 0.2.31 / 055 原创视觉、动画与声音增量

| 新增或调整内容 | 可审查来源 | 来源与许可边界 |
|---|---|---|
| 人物腰肩/外套/靴/帽与宽队色，马匹/骑手/刀枪，Cannon/Mortar，建筑用途轮廓，F/W/C箱内容 | [ProceduralArt.cs](../Assets/Frontier/Presentation/ProceduralArt.cs) | 本项目手工编写的截面、顶点/三角形、基础几何与纯色材质；未使用下载模型、纹理、扫描或生成式图像。既有火枪手FBX继续按0.2.4原创源记录。 |
| 程序化步态、坐姿、瞄准/拉弦/挥击/后座/恢复/部署、工作工具、受伤/死亡表现 | [UnitAnimation.cs](../Assets/Frontier/Presentation/UnitAnimation.cs)、[UnitRig.cs](../Assets/Frontier/Presentation/UnitRig.cs)、[WorkerEquipment.cs](../Assets/Frontier/Presentation/WorkerEquipment.cs)，整体姿态沿用WorldView | 本项目编写的刚性枢轴与时间函数；没有第三方动画片段、动作捕捉或外部骨骼资产。实际支持范围及未实现内容见Art-Animation-055。 |
| 六种战略提示音 | [NoticeSoundClips.cs](../Assets/Frontier/Presentation/NoticeSoundClips.cs) | 原创频率序列、谐波与包络直接生成22050Hz单声道AudioClip，无录音或商业音效输入。 |
| 环境风床与原创主题音乐 | [AmbientAudio.cs](../Assets/Frontier/Presentation/AmbientAudio.cs) 的AmbientSoundClips | 本项目平滑噪声、四组和弦频率与八音旋律直接合成16秒风声、24秒音乐循环；未引用外部采样文件或现成乐谱。 |
| URP研究图与原生监听器诊断音频 | FrontierArtStudy055Tests、NativeAudioCapture读取当前模型/AudioListener | 输出来自项目自己运行的渲染/声音；图中文字使用既有Noto字体，仍保留OFL。原始截图/诊断WAV不是新下载素材，也不单独证明正式美术、混音或玩家验收。 |

2026-10-02重新计算：NotoSans-Regular.ttf、FrontierInfantry.fbx、FrontierInfantry.blend的大小与SHA-256均与前文记录一致。055没有新增需另附许可的外部模型、贴图、动画、音乐文件或字体；Unity及各包许可不会因为模型/声音由代码生成而失效。

本页是素材来源和随附许可清单，不单独授予第三方复用本项目原创代码、模型或音乐的许可，也不改变项目所有者的发布授权。字体继续遵守OFL；Unity包与其中第三方组件继续按各自随附全文使用，不能把原创资产来源说明扩展为对引擎、编辑器或所有依赖的通用再分发授权。统一定向67/67与完整214/214 EditMode、210/210 PlayMode已通过，新5组URP图已读审；新原生客户端音频与正常玩家验收仍在进行，不宣称阶段D已通过。


## 0.5.0 / 061 河流增量

WaterPresentation.cs 的河面程序波纹/法线、河岸、木桥、码头、捕鱼船、炮艇与鱼群均由本项目 C# 几何和程序材质生成；沿用 ArchitecturalGeometry 与 ArchitecturalMaterials。未导入商业游戏模型、纹理、音效或下载素材，无新外部依赖。Evidence/061 的图片来自本工程实际 URP/原生客户端，具体场景与夹具性质由配套 JSON 标明。既有字体、Unity及依赖许可全文继续适用。


## 0.9.0 / 066 Chinese guided-play font

`Assets/Resources/Fonts/NotoSansCJKsc-Regular.otf` is the official Noto Sans CJK SC Regular font, pinned to the notofonts/noto-cjk commit `f8d157532fbfaeda587e826d4cd5b21a49186f7c`. The 16,437,364-byte asset has SHA-256 `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`. [Pinned provenance](Licenses/Noto-CJK-provenance.json) records both upstream URLs and hashes; the complete [SIL Open Font License 1.1](Licenses/Noto-CJK-OFL.txt) is included in source and client license packages. It supplies Chinese glyphs for the project's original guided-play text; no commercial game font or campaign assets are imported.
