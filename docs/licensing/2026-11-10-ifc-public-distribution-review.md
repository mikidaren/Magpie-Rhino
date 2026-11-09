# 0.4.1 完整离线 IFC 二进制公开分发核查

核查日期为 2026-11-10。目标为可公开分发的完整 Windows x64 离线运行时。当前结论为 **blocked**。优先方案为记录完整输入的 **OCC-only 替代源码构建**，配套实际对应源码、notice 与可替换共享库机制。现有完整预编译包的组合许可和构建对应关系仍有具体缺口。

用户本轮授权在 workspace 获取官方源码与许可材料。本轮补齐五份源码，合计 72277695 字节。缓存目录现有 30 个已记录材料，合计 514769086 字节，其中 28 个为源码、构建、子模块或数据归档，两个为独立许可证文件。每个下载地址、长度和 SHA-256 保存在 `packaging/ifc-runtime/source-artifacts.json`。

本报告的逐项证据、归档内路径、成员摘要和验收缺口保存在 `packaging/ifc-runtime/public-distribution-review-0.4.1.json`。以下 E 编号对应该文件的本地证据，W 编号对应已读取的权威原始网页。

## 1. 本次实际核查对象

读取的候选目录为 workspace 内 `work/offline final candidate 0.4.1/openbim/runtime`。其 manifest 大小为 492042 字节，列出 2666 个文件，SHA-256 为 `d319b422e296c5d0ab077f74a3a1e7f33a34ff5b92d1727d27cf19c52baa88a4`。本结论绑定该快照。

- CPython 标识为 3.12.13，IfcOpenShell wheel 标识为 0.9.0.post1，native wrapper 的已有核查标识为 `0.9.0post1+b5e509b`。
- 两个 CGAL kernel、SVG-fill、Manifold 和额外的 `Lib/site-packages/ifcopenshell/python312.dll` 均在快照中。额外 Python DLL 为 3.12.1。
- 根目录随包分发 `msvcp140.dll`，版本 14.44.35229.0，557416 字节，SHA-256 为 `0fa7eb792d3fbcf2233e4ea47e9144b9b1309ba8ed5d4731a72fffa8f4f556d6`。两个 `vcruntime140*.dll` 为 14.44.35211.0。
- 该快照的 notice manifest 为 10 个主组件和 24 个附属组件。本次补正后的 catalogue 为 10 个主组件和 26 个附属组件。父任务需要从实际 staging 重收集 notice，再重算 runtime manifest 和安装包摘要。

## 2. 条文义务与证据判据

### IfcOpenShell

wheel 元数据声明 `LGPL-3.0-or-later`。对应 native commit 为 `b5e509b223e1ab2c7eb4641211ea05209a2dfa3e`。其源码归档、GPL 和 LGPL 正文已经取得并校验。E01 的 LGPLv3 第 4 节要求显著使用声明、GPL/LGPL 文本，以及适用的版权展示、修改与调试修改所需反向工程权限。共享库机制应能运行接口兼容的修改版本，另一路径为最小对应源码与可重新组合或链接的应用材料。安装信息按第 4(e) 节及 GPLv3 第 6 节的具体触发条件评估。W02、W06。[LGPLv3 原文](https://www.gnu.org/licenses/lgpl-3.0.en.html) [FSF 静态和动态链接说明](https://www.gnu.org/licenses/gpl-faq.en.html#LGPLStaticVsDynamic)

已有外部 `.py`、`.pyd`、DLL 和可重新生成的 manifest 为替换机制提供技术基础。验收还需要替换兼容修改库后的实际运行记录，以及与该库对应的源码和构建材料。原始 commit、wheel 版本与元数据仅证明各自已经观察到的事实。实际使用的补丁、依赖和控制构建脚本仍需形成可核对的对应关系。

GPLv3 第 1 节以用于生成、安装、运行和修改的源码及脚本定义对应源码。验收应关注真实输入和改动记录。字节级可复现可提供额外强证据。W03 明确说明实际对应源码与完全相同二进制摘要属于不同判据。E02、W03。[FSF 对应源码与摘要说明](https://www.gnu.org/licenses/gpl-faq.en.html#MustSourceBuildToMatchExactHashOfBinary)

### CGAL

缓存的 CGAL 5.5.5 `Installation/LICENSE` 指向逐文件的 GPLv3+/LGPLv3+ 条款。Nef 和 Polygon Mesh Processing 的已核查头文件均明确标注 `GPL-3.0-or-later OR LicenseRef-Commercial`。E03。CGAL 官方许可说明也按包区分基础 LGPL 部分与较高层 GPL 算法，并说明商业授权路线。W01。[CGAL 官方许可说明](https://www.cgal.org/license.html)

当前二进制包含 `ifcopenshell_geometry_kernel_cgal.dll`、`ifcopenshell_geometry_kernel_cgalsimple.dll` 和 `ifcopenshell_geometry_svgfill.dll`。SVG-fill 的 CMake 目标明确链接 `IFCOPENSHELL_CGAL` 与 LibXml2，核查范围必须覆盖它。E04。

采用 GPL 路线时，需要确定实际组合工作的范围，并满足相应源码、许可证、修改声明和分发条件。Python worker、native library 和插件之间的调用及数据结构需要分别评估。外部进程边界的验收取决于真实通信语义。FSF 的说明同时涉及动态插件函数调用、共享数据结构和进程通信。W05。[FSF 插件与组合工作说明](https://www.gnu.org/licenses/gpl-faq.en.html#GPLPlugins)

商业许可或额外许可路线需要覆盖所用版本、代码和分发方式的实际授予文件。现有材料尚无该授予凭据。选择 GPL 兼容分发边界、实际排除相关代码、取得适用授予，是各自需要具体证据的路线。

### OpenCASCADE

`TKernel.dll` 的文件和产品版本为 7.8.1，与 native commit 的 Windows recipe 一致。OCCT V7_8_1 完整源码、LGPL 2.1 正文和 `OCCT_LGPL_EXCEPTION.txt` 已取得。E05、E06。

OCCT 例外允许使用其头文件材料的对象代码按所选条款分发，并要求显著说明 OCCT 使用情况。对实际携带的库、修改及共享库替换，仍按适用 LGPL 条件核查。LGPL 2.1 第 6 节包含显著使用声明、许可证副本、相关版权展示、修改和调试修改所需反向工程权限，以及共享库或重新链接选项。第 6(d) 节提供同处等价下载材料的路径。E06。

native recipe 的 `win/patches/V7_8_1.patch` 为 4112 字节，SHA-256 为 `6bc4d73cab185f99ffb09b51b07d7e2499f72a6a5685c8523490f0a39e2e3640`。对应源码交付需覆盖实际采用的补丁、布局改动和构建配置。已缓存的 patch 证明该改动存在于 recipe；与分发 DLL 的适用关系仍在 G1 中核查。

### MPIR 与 MPFR

MPIR 的 `gmp-h.in` 和 MPFR 的 `src/mpfr.h` 均声明 LGPLv3 或以后版本。E07、E08。MPIR recipe 固定 commit `5e0c2061af105c151970d41c8394ce956f77e455`，还应用 `mpir.patch` 和 `mpir_runtime.patch`。MPFR recipe 取可变分支 `ifcopenshell`，再应用 `mpfr.patch`。E09。

当前缓存 MPFR commit 为 `9f637c103431faa235f1aad7e35b639d3aa635ee`，其头文件声明 `4.1.0-dev`。这是取得的候选源码版本。该分支快照与 wheel 构建时输入的关系仍待证明。

若这些库静态进入保留的 native 对象，需要交付实际使用的源码、修改及适用的可重新链接应用材料。只有外部 DLL 可替换的证据，覆盖不到内部静态库的重新链接要求。将包含相关实现的目标从受控构建输入中实际排除，也可改变对应义务范围。E01、E09、W06。

### MSVC 运行库

当前 app-local `msvcp140.dll` 保持与 Shapely wheel 中已核查文件相同的字节。CPython 实际 `LICENSE.txt` 第 286–320 行包含 Microsoft Distributable Code 说明及再分发条件。Shapely 的 Windows notice 引用 2015 RC 页面。本次对当前版本运行库的权利核查，仍需要匹配的再分发依据和适用 edition 条款。

Microsoft 当前再分发清单说明，适用权利以有效许可及具体软件条款为条件，所列 runtime 文件应保持未修改，`debug_nonredist` 属于禁止再分发目录。当前文件的摘要和签名来源只覆盖字节与来源事实。具体分发权利需要记录授予依据。W07。[Microsoft Visual Studio 2022 再分发清单](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution)

保留 GPL CGAL 的方案另有一项组合审查。FSF Windows FAQ 将动态链接系统 runtime 与随 GPL 程序分发其 DLL 区分处理，并对后者使用 System Library 例外给出严格限制。该说明应与实际 GPL/LGPL 条文和具体分发组合分别核对。Microsoft 的文件分发权与 GPL 组合条件需要各自满足。W04。[FSF Windows runtime 与 GPL 说明](https://www.gnu.org/licenses/gpl-faq.en.html#WindowsRuntimeAndGPL)

OCC-only 路线采用适用的 LGPL 共享库及源码交付机制，保留当前完整离线运行时所需 MSVC 文件时，仍需完成 Microsoft 授予和具体条款核查。将 MSVC 改作宿主预装依赖会改变自包含验收假设，必须另行验证宿主保证和真实安装环境。

## 3. 本轮补正与已闭合材料缺口

| 项目 | 新证据及处理 | 剩余范围 |
| --- | --- | --- |
| Eigen 3.3.9 | 官方完整源码与六个根部许可/readme 文件已取得 | 公共 `IfcGeom` 与 OCC kernel 均使用 Eigen。需记录实际头文件范围。`EIGEN_MPL2_ONLY` 可在受控构建中拒绝非 MPL2 模块。E10、E11 |
| Boost 1.92.0 | 官方 52633196 字节 b2-nodocs 完整源码已取得，`BOOST_VERSION` 为 109200，含 container 源码 | 构建输入与实际 native 对象的对应范围仍需记录。BSL 的独立 notice 义务与更大 copyleft 对应源码的输入需求分别核查 |
| RocksDB | 保留的 parse DLL 和 wrapper 含 `_deps/rocksdb-9.11.2` 路径、构建时间和 `rocksdb_build_git_sha:0`。已取得 9.11.2 源码、Apache 与 LevelDB BSD 文本 | 先前目录漏记了此 core 证据。catalogue 选用允许的 Apache 路线并保留 BSD 文本。原生源码修改、压缩依赖与静态范围仍需对应。E12 |
| Zstd | recipe 为 RocksDB 启用 Zstd，现有核心对象含其压缩标识。已取得候选 1.5.7 源码与实际 BSD 文本 | `lib/zstd.h` 明确允许 BSD/GPLv2 二选一。catalogue 选择 BSD 库条款。实际编译版本与功能范围保留资格说明。E13 |
| Expat 2.7.4 | 已有 CPython 3.12.13 源码中 `Modules/expat/expat.h` 明确为 2.7.4，已改收其实际 `COPYING` | 原先以通用下载字典 2.6.3 推出的 Windows 差异已获得源码解释。E14 |
| libffi | 实际 Windows `build_libffi` 固定 `python/cpython-source-deps` commit `16fad4855b3d8c03b5910e405ff3a04395b39a98`，已取得该源码及 MIT 文本，`configure.ac` 为 3.4.2 | 通用 3.4.6 字典与此 Windows 路径分开。DLL 缺少文件版本资源，目录保留源码候选与关联资格说明。E15、E16 |

CPython/Boost/MIT/BSD 等宽松许可项按实际条款保留版权、许可及修改说明。完整上游源码归档同时用于源码覆盖与可重建材料准备。保留的 GEOS、Eigen 文件级范围和 NumPy 内 GCC runtime exception 的具体适用依据，也纳入完整包 G6 核查。

## 4. 可交付发布路线

### P1 受控 OCC-only 替代构建，优先

本方案保留现有独立 worker 与 OCC 几何职责。已缓存 IfcOpenShell、OCCT、Eigen、Boost 与 CPython 源码。下列开关来自 native commit 的真实 CMake 定义，表示待验证的构建意图。E17。

```text
WITH_OPENCASCADE=ON
WITH_CGAL=OFF
WITH_MANIFOLD=OFF
COLLADA_SUPPORT=OFF
GLTF_SUPPORT=OFF
WITH_PROJ=OFF
USD_SUPPORT=OFF
WITH_ROCKSDB=OFF
WITH_ZSTD=OFF
```

另外为 Eigen 用户记录并使用 `EIGEN_MPL2_ONLY`。记录完整编译输入、所用 patch、头文件、工具版本、链接库、feature defines 和打包裁剪清单。源码树本身提供这些功能开关；本轮尚无该配置的新编译产物和新包 smoke 结果。

验收需证明禁用功能在真实编译/链接输入和最终包中的排除情况，保留 OCC 相关功能所需的 schema/mapping/core DLL，使用适用 Microsoft 权利分发 runtime，并运行最终安装包的全部既有离线 worker 场景。实际生成版本、ABI 和 resolver 支持身份需要一致验证。

### P2 对现成 wheel 作插件裁剪

上一轮临时副本的 22 文件省略试验通过了两层模型的 export 与 inspect，共省略 23134488 字节。该结果覆盖限定 worker 场景。当前快照仍携带这些文件。

许可范围缩减至少同时检查两个 CGAL kernel 和 SVG-fill。移除 Manifold、额外序列化器或 Boost DLL 时，需要同步更新实际 presence、指纹和 notice。公共 Eigen、parse/wrapper 内 RocksDB 等核心内容仍需核查。原始 native 对象与实际源码/修改的关联依旧需要 G1 证据。

因此，本方案可作为可验收的范围缩减步骤。其完成标准还包括剩余核心源码和许可覆盖。

### P3 保留 CGAL 并采用 GPL 兼容分发范围

需确定真实组合工作边界，提供适用 GPL 范围的完整源码和修改、MPIR/MPFR 等静态重新链接材料，并单独解决随包 Microsoft runtime 的许可组合条件。现有 worker/第三方 MIT 材料可进入相应 GPL 兼容范围，具体分发选择需要清晰记录。当前 P3 的范围和 runtime 条件尚未完成。

### P4 取得商业或额外授予

实际授予需要覆盖所用 CGAL 版本、算法、对象和分发方式。该路线还需完成独立的 LGPL、源码、notice 和 Microsoft 义务。当前授权材料中缺少相关授予文件。

## 5. 源码与 notice 交付方案

对网络二进制发行，采用实际配对的 source asset 和等价下载路径。GPLv3 第 6(d) 节及 LGPL 2.1 第 6(d) 节提供依据。源文件可与二进制位于不同服务器，但需要在二进制下载处给出清晰指引并维持对应材料可用。E02、E06、W08。[FSF 源码与二进制下载位置说明](https://www.gnu.org/licenses/gpl-faq.en.html#SourceAndBinaryOnDifferentSites)

拟交付材料应包含以下实际内容。

1. 最终二进制安装包、runtime manifest 和 SHA256SUMS。
2. 与该二进制范围对应的源码归档、实际 patch、构建控制脚本、配置和修改清单。
3. `SOURCE-COVERAGE.json`，逐个保留组件列出版本、binary paths、source archive/member、SHA-256、改动与对应证明。
4. `NOTICE`、原始许可证/版权、权利范围说明和适用的共享库替换或重新链接材料。
5. 直接可取得的 source asset 下载指引和持续可用责任记录。

当前 `source-artifacts.json` 是取得材料的证据目录。最终 `SOURCE-COVERAGE.json` 需要围绕实际分发对象逐项补齐。源码资产同时包含受 LGPL 条件保护的自有修改，并按适用条款保留每个组件的许可。仅提供上游首页或缺少依赖/patch 的产品仓库快照，覆盖范围不足以完成这里的对应判据。

交付按第 6(d) 节的实际源码提供机制准备。三年书面承诺机制属于另行选择的分发路线，需要真实履行主体、条件和执行能力。本报告记录的路线选择与准备材料均保持在公开发行审批之前。

## 6. 当前缺口与完成判据

| 编号 | 尚有的阻碍 | 可接受的完成证据 |
| --- | --- | --- |
| G1 | 现成 native 对象的实际源码、改动和构建输入关联 | 覆盖保留对象的上游构建说明、源码/patch 清单，或记录输入的受控替代构建。逐位复现可作为附加强证据 |
| G2 | CGAL 的组合分发范围与适用路线 | 完整验证的 OCC-only 排除/构建，或完成的 GPL 兼容范围和 runtime 条件，或真实授予 |
| G3 | MPIR/MPFR 的实际静态范围与 MPFR 构建时 commit | 匹配 revision、patch、可重新链接材料，或保留对象中实际排除的证据 |
| G4 | 当前 MSVC 文件的再分发权利及 GPL 路线随包条件 | 与这些版本对应的有效授予/适用条款、来源和保护条件记录，以及适用组合依据 |
| G5 | 真实对应源码资产与 LGPL 用户权限/替换 | 完整 source asset、配对指引、权利说明和实际兼容替换或重新链接验证。安装信息按具体触发条件处理 |
| G6 | 保留的 Eigen 文件级范围、RocksDB/压缩范围、GEOS 与 GCC 例外 | 源码与编译/链接清单、实际原文 notice 和适用依据，逐项覆盖最终对象 |
| G7 | 现成候选包仍含旧 notice 目录 | 用实际 staging 重收集并验证全部许可文本，随后重算 runtime manifest 和最终安装包摘要 |

本轮补正 catalogue 已从实际 staged 运行时重新收集到独立输出目录。16 项 collector 测试通过，实际输出含 10 个主组件、26 个附属组件、73 份逐字节摘要验证的许可文本和 147 个 native 文件摘要。该输出位于 workspace 的 `work/ifc-source-cache/public-distribution-notices-0.4.1-review`，门禁为 `blocked`。父任务仍需把新材料纳入最终 runtime，并重算相应 manifest 和安装包。

本轮可交付内容为补正 catalogue、30 个取得材料的摘要目录、逐条证据索引和以上发布路线/验收条件。公开目标已经记录；对上述快照的发行准备状态仍为 **blocked**。
