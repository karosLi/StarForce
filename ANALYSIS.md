# StarForce 资源管线深度剖析

本文档对 StarForce 项目（基于 GameFramework 架构）的资源构建与运行时管线进行全面、端到端的深度剖析。

---

## 第一部分：构建阶段 —— 资源是如何被打成包的

构建阶段是一个多步骤的过程，它将 Unity 编辑器中的资产（Assets）转换为可部署的 AssetBundle 文件以及管理它们所需的清单（Manifest）文件。

### 1.1. 构建哲学：声明式的、基于目录的策略

整个流程的起点是一个单一的、声明式的“事实来源”：**`Assets/GameMain/Configs/ResourceCollection.xml`**。

*   **核心策略：** 项目采用了 **“目录即AB包”** 的策略。XML中的每一个 `<Resource>` 标签（例如 `<Resource Name="Entities" ... />`）都直接对应于 `Assets/GameMain/` 内的一个文件夹。这个指令告诉构建系统去查找该文件夹内的所有资产，并将它们打包进一个单独的 AssetBundle 中。
*   **粒度与权衡：** 这种策略导致了混合的打包粒度。虽然易于管理，但它给热更新带来了一个权衡。例如，由于所有贴图都在一个单独的 `Textures` 包里，即使只修改一张很小的贴图，也需要用户重新下载整个可能非常大的 `Textures` 包。
*   **资产映射：** XML 中的 `<Assets>` 部分作为一个明确的、低级别的清单，将每个资产的唯一GUID映射到其指定的 AssetBundle（`ResourceName`）。这个列表由框架的 `Resource Editor` 编辑器窗口自动管理，为项目的资产和构建配置之间提供了稳固的链接。

### 1.2. 构建配置：受控且可定制

构建过程的技术参数在 **`Assets/GameMain/Configs/ResourceBuilder.xml`** 文件中定义。

*   **压缩方式：** 系统配置为使用 **LZ4 压缩** (`<AssetBundleCompression>1</AssetBundleCompression>`)。这是大多数项目的最佳选择，它在文件大小和快速加载性能之间取得了良好的平衡。
*   **版本控制：** 构建版本由一个 `InternalResourceVersion` 整数来控制，每次构建时该整数都会递增。这个简单的整数是运行时版本比较的基石。
*   **增量构建：** 配置中指定了构建应为增量式的 (`<ForceRebuildAssetBundleSelected>False</ForceRebuildAssetBundleSelected>`)，这通过仅重新构建已更改的资产来加速开发迭代。
*   **自定义钩子：** 最关键的是，配置指定了一个自定义的构建事件处理器：`<BuildEventHandlerTypeName>StarForce.Editor.StarForceBuildEventHandler</BuildEventHandlerTypeName>`。这告诉构建控制器在管线的关键时刻执行项目特定的代码。

### 1.3. 构建引擎：`ResourceBuilderController.cs`

此脚本是构建过程的核心，负责执行在XML文件中定义的策略。

1.  **依赖性分析：** 控制器首先调用 `ResourceAnalyzerController`。该子模块系统地使用 Unity 的 `AssetDatabase.GetDependencies()` API 来构建一个包含所有资产及其依赖关系的完整映射图。此信息随后被烘焙到版本清单中。
2.  **AssetBundle 创建：** 接着，它调用 `BuildPipeline.BuildAssetBundles()`，将 XML 定义转换为 Unity API 所需的 `AssetBundleBuild[]` 数组。此命令在临时的“工作目录”中创建原始的 AssetBundle 文件。
3.  **清单生成：** 这是该控制器最至关重要的职责。在AB包构建完毕后，它会遍历每一个包，并计算其文件大小和 CRC32 哈希值（包括未压缩和压缩两种版本）。然后，它将所有这些信息——包括依赖关系、哈希值和大小——序列化成一个自定义的二进制文件：**`GameFrameworkVersion.dat`**。这个主清单是整个热更新系统的关键。
4.  **缓存清除（Cache-Busting）：** 为了在 CDN 上进行部署，最终的 AssetBundle 文件和主清单文件会被重命名，以包含其哈希值（例如，`textures.ab.a1b2c3d4.dat`）。

### 1.4. 最后一步：`StarForceBuildEventHandler.cs`

这个自定义脚本执行了为游戏初次安装做准备的最后一个关键步骤。

*   **构建前清理：** 在构建开始之前，`OnPreprocessAllPlatforms` 方法会运行并 **完全清空 `Assets/StreamingAssets` 文件夹**。这确保了之前构建的旧的或过时的文件不会被意外地包含在新的游戏包中。
*   **构建后复制：** 在构建成功完成后，`OnPostprocessPlatform` 方法会运行。它会获取新生成的“Package”资源（为基础游戏版本准备的一组AB包和清单），并将它们 **复制到刚刚被清空的 `Assets/StreamingAssets` 文件夹中**。这保证了每一个新的应用程序构建都包含一套全新的、正确的“基础”资源。

---

## 第二部分：运行时阶段 —— 资源是如何更新和加载的

运行时阶段由一系列“流程”（一个状态机）控制，这些流程向核心的 `ResourceComponent` 发出指令来执行操作。

### 2.1. 版本比较 (`ProcedureCheckVersion.cs`)

这是游戏启动时的第一步。

1.  该流程向服务器URL发送一个Web请求，以获取最新的版本信息。
2.  成功后，它将服务器的响应解析为一个 `VersionInfo` 对象。
3.  它首先检查 `ForceUpdateGame` 标志。如果为真，它会停止热更新流程，并提示用户从应用商店下载新客户端。
4.  然后，它调用 **`GameEntry.Resource.CheckVersionList(serverInternalResourceVersion)`**。`ResourceComponent` 会比较服务器的资源版本号与设备上当前的本地资源版本号。
5.  根据比较结果，游戏将转换到 `ProcedureUpdateResources` 状态（如果需要更新）或 `ProcedureInitResources` 状态（如果所有内容都是最新的）。

### 2.2. 清单更新与增量下载 (`ProcedureUpdateResources.cs`)

此流程处理下载过程。

1.  底层的 `ResourceComponent` 首先从服务器下载新的主清单（`GameFrameworkVersion.dat`）。
2.  它将这个新的服务器清单与本地清单进行比较，以生成一个需要更新的 AssetBundle 的“差异”列表（这些AB包是全新的或哈希值不同）。
3.  然后，它将此差异列表中的每个AB包添加到一个由 **`DownloadComponent`** 管理的下载队列中。
4.  `DownloadComponent` 使用一个 `UnityWebRequestDownloadAgentHelper` 工作线程池来同时下载最多3个文件。
5.  在文件下载过程中，`ProcedureUpdateResources` 脚本会监听事件并更新UI进度条，以向用户提供反馈。
6.  所有下载完成后，新的清单被保存，游戏转换到下一个流程。

### 2.3. 加载流程 (`ResourceComponent.cs` & `DefaultLoadResourceAgentHelper.cs`)

这是在更新过程完成后，资产如何被加载到游戏中的过程。

1.  **请求：** 游戏代码通过一个工具方法请求资产，例如 `AssetUtility.GetUIFormAsset("MenuForm")`，这会解析成一个完整的路径，如 `"Assets/GameMain/UI/UIForms/MenuForm.prefab"`。此路径被传递给 `GameEntry.Resource.LoadAsset()`。
2.  **查找：** `ResourceComponent` 接收到请求。它查询其已加载的清单，以确定该资产位于 `UI/UIForms` 这个 AssetBundle 中。
3.  **依赖解析：** 然后，它在清单中检查 `UI/UIForms` 包的所有依赖项（例如，它可能依赖于 `common_shaders` 包）。它会递归地确保所有的依赖包都首先被加载。
4.  **AssetBundle 加载：** 一个 `DefaultLoadResourceAgentHelper` 从池中被分派出来加载 `UI/UIForms` AssetBundle。它使用 **`AssetBundle.LoadFromFileAsync(path)`**，这是从磁盘加载AB包的最高效的方式。路径将指向 `StreamingAssets` 文件夹（对于基础资源）或持久化数据路径（对于已更新的资源）。
5.  **资产加载：** 一旦AB包被加载，该助手会立即调用 **`assetBundle.LoadAssetAsync("Assets/GameMain/UI/UIForms/MenuForm.prefab")`** 来从包中获取特定的预制体对象。
6.  **缓存与返回：** 加载的 AssetBundle 和加载的预制体都被放入由 `ResourceComponent` 管理的对象池中。这确保了后续对该预制体（或同一AB包中的任何其他资产）的请求几乎可以立即从内存中返回。最终的预制体通过回调函数返回给游戏代码。

这整个管线，从构建到运行时，展示了一个健壮、专业且高度自动化的游戏资源管理系统，实现了高效的开发和可靠的热更新。
