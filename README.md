# 立筒仓物料管理系统

------

## 背景简介

随着物联网技术的发展和移动终端的普及，**立筒仓测量系统**的用户希望能够利用移动终端或者电脑时刻获取立筒仓信息和服务，想要实现用户的需求，就需要解决物联网硬件设备与各种操作系统和平台间信息如何交互问题。
![Image text](https://github.com/sdzjc070/Project_0001/blob/master/image/%E7%99%BB%E5%BD%95.png?raw=true)

### 1. **体系结构设计**
本项目设计出的物联网框架由四个层次构成：分别是设备感知层、基础服务层、数据传输层，软件应用层：
![Image text](https://github.com/sdzjc070/Project_0001/blob/master/image/%E6%9E%B6%E6%9E%84%E5%B1%82%E6%AC%A1%E5%9B%BE.png?raw=true)
**设备感知层**：由一系列传感器装置组成的测量设备和由无线模块组成的控制设备构成，用于搜集立筒仓内监控数据并发送。测量设备收集温度、湿度、激光测量传感器收集的数据，控制设备对测量数据进行计算并组装成指令信息使用串口发送出去。
**基础服务层**：为整套系统架构的核心，本论文将服务程序编写为Windows服务程序安装在企业办公电脑中，用于组装指令和解析数据，将数据保存在本地Sqlserver或者MySql数据库中。同时分别给Windows客户端程序和ReactNative手机程序提供API接口。并且本层接收应用层的远程控制请求，对获取到的立筒仓测量信息进行分析处理，提供给应用层进行数据访问和远程控制两大主要功能。
**数据传输层**：在传输层中有两套传输方式，由于Windows客户端安装在企业本地电脑中，所以利用Microsoft Message Queue消息队列传输消息。当用户利用手机通过互联网进行远程控制和访问数据时，使用MQTT方式传输，在此层中搭建MQTT代理服务器。这样设计的好处是保证了有网情况下，可以通过Windows客户端、手机等方式远程控制。无网情况下，可以直接使用Windows客户端进行操作，满足最低要求。避免了在无网环境下，设备不能使用的情况。
**软件应用层**：基于之前的第一层感应层和第二次服务层，系统获取到了料仓中的物理参数和数据分析之后的详细数据。为了能够通过应用程序实现远程控制，同时对数据进行更加人性化的展示，本框架设计了基于C#的Windows客户端和基于ReactNative的Android手机程序，同时为用户提供风格一致，操作简单的应用界面。通过数据分析计算，Windows客户端绘制出立筒仓物料形状的二维图像，更加直观的展现数据。

### 2. **Windows服务设计与实现**

Windows服务主要用于运行在Windows系统后台。解析无线模块接收到的硬件设备传来的指令和数据，将需要保存的数据保存在本地数据库中。组装控制信息指令通过无线通信模块发送给测量设备。Windows服务程序设计为开机启动，所以每时每刻给Windows客户端程序和手机程序提供访问接口。
![Image text](https://github.com/sdzjc070/Project_0001/blob/master/image/%E5%8A%9F%E8%83%BD%E6%A8%A1%E5%9D%97%E5%9B%BE.png?raw=true)

### 3. **Windows客户端设计与实现**

立筒仓测量系统Windows客户端，安装在企业计算机中，用户使用客户端通过消息队列向Windows服务程序发送和接收各种操作指令，并将获取到的信息显示在客户端页面中。用户通过客户端可远程控制工厂立筒仓中安装的测量设备，进行盘库和清洁镜头操作。实时监控盘库进度，清洁镜头进度等信息，以此方便用户获取立筒仓中物料的体积，温湿度等数据, 系统功能架构设计。
客户端提供给用户两个主要功能模块：
1、远程控制料仓盘库设备。用户可以控制设备进行盘库、清洁镜头操作。用户点击盘库或者清洁镜头按钮，控制设备进行操作，在这过程中客户端每隔半分钟向控制设备发送请求状态指令，获取实时的设备信息。等到设备回复当前状态之后，客户端更新软件界面，显示设备最新的状态和操作进度。当盘库完成，客户端自动获取设备的测量信息，并保存在数据库中。
2、历史数据查看与分析。用户可以查询历史记录，对测量数据进行评价和分析。用户可以查看不同设备的历史记录，并通过软件对测量的信息进行计算，展示出料仓中的物料表面形状。对一段时间内的数据信息进行统计，以图表的形式形象的展示给用户。
整个软件提供给用户方便的操作，代替人工对立体筒仓进行盘库，保证了人员的安全，提高了盘库的准确性。同时将测量结果保存在计算机数据库中，保证数据的安全，方便员工实时查看。其中数据分析功能，更好且直观的呈现给用户立体筒仓内物料的高度，料面的形状。
![Image text](https://github.com/sdzjc070/Project_0001/blob/master/image/%E5%AE%A2%E6%88%B7%E7%AB%AF%E5%8A%9F%E8%83%BD%E6%A8%A1%E5%9D%97.png?raw=true)

### 4. **功能展示**

**4.1** 立筒仓测量
立筒仓远程控制测量，俗称盘库，盘库具体流程图：
![Image text](https://github.com/sdzjc070/Project_0001/blob/master/image/%E7%9B%98%E5%BA%93%E6%B5%81%E7%A8%8B%E5%9B%BE.png?raw=true)
**4.2**客户端主界面
![Image text](https://github.com/sdzjc070/Project_0001/blob/master/image/%E7%B3%BB%E7%BB%9F%E7%95%8C%E9%9D%A2%E5%9B%BE.png?raw=true)
**4.3**仓内数据实时监控
![Image text](https://github.com/sdzjc070/Project_0001/blob/master/image/%E5%AE%9E%E6%97%B6%E7%9B%91%E6%8E%A7.png?raw=true)
**4.4**历史测量数据管理
![Image text](https://github.com/sdzjc070/Project_0001/blob/master/image/%E6%95%B0%E6%8D%AE%E7%AE%A1%E7%90%86%E5%9B%BE.png?raw=true)
**4.5**仓内物料图像
![Image text](https://github.com/sdzjc070/Project_0001/blob/master/image/%E6%B5%8B%E9%87%8F%E4%BA%8C%E7%BB%B4%E5%9B%BE.png?raw=true)

### 5. 高效绘制 [序列图](https://www.zybuluo.com/mdeditor?url=https://www.zybuluo.com/static/editor/md-help.markdown#8-序列图)

```seq
Alice->Bob: Hello Bob, how are you?
Note right of Bob: Bob thinks
Bob-->Alice: I am good thanks!
```

### 6. 高效绘制 [甘特图](https://www.zybuluo.com/mdeditor?url=https://www.zybuluo.com/static/editor/md-help.markdown#9-甘特图)

```gantt
    title 项目开发流程
    section 项目确定
        需求分析       :a1, 2016-06-22, 3d
        可行性报告     :after a1, 5d
        概念验证       : 5d
    section 项目实施
        概要设计      :2016-07-05  , 5d
        详细设计      :2016-07-08, 10d
        编码          :2016-07-15, 10d
        测试          :2016-07-22, 5d
    section 发布验收
        发布: 2d
        验收: 3d
```

### 7. 绘制表格

| 项目        | 价格   |  数量  |
| --------   | -----:  | :----:  |
| 计算机     | \$1600 |   5     |
| 手机        |   \$12   |   12   |
| 管线        |    \$1    |  234  |

### 8. 更详细语法说明

想要查看更详细的语法说明，可以参考我们准备的 [Cmd Markdown 简明语法手册][1]，进阶用户可以参考 [Cmd Markdown 高阶语法手册][2] 了解更多高级功能。

总而言之，不同于其它 *所见即所得* 的编辑器：你只需使用键盘专注于书写文本内容，就可以生成印刷级的排版格式，省却在键盘和工具栏之间来回切换，调整内容和格式的麻烦。**Markdown 在流畅的书写和印刷级的阅读体验之间找到了平衡。** 目前它已经成为世界上最大的技术分享网站 GitHub 和 技术问答网站 StackOverFlow 的御用书写格式。

---

## 什么是 Cmd Markdown

您可以使用很多工具书写 Markdown，但是 Cmd Markdown 是这个星球上我们已知的、最好的 Markdown 工具——没有之一 ：）因为深信文字的力量，所以我们和你一样，对流畅书写，分享思想和知识，以及阅读体验有极致的追求，我们把对于这些诉求的回应整合在 Cmd Markdown，并且一次，两次，三次，乃至无数次地提升这个工具的体验，最终将它演化成一个 **编辑/发布/阅读** Markdown 的在线平台——您可以在任何地方，任何系统/设备上管理这里的文字。

### 1. 实时同步预览

我们将 Cmd Markdown 的主界面一分为二，左边为**编辑区**，右边为**预览区**，在编辑区的操作会实时地渲染到预览区方便查看最终的版面效果，并且如果你在其中一个区拖动滚动条，我们有一个巧妙的算法把另一个区的滚动条同步到等价的位置，超酷！

### 2. 编辑工具栏

也许您还是一个 Markdown 语法的新手，在您完全熟悉它之前，我们在 **编辑区** 的顶部放置了一个如下图所示的工具栏，您可以使用鼠标在工具栏上调整格式，不过我们仍旧鼓励你使用键盘标记格式，提高书写的流畅度。

![tool-editor](https://www.zybuluo.com/static/img/toolbar-editor.png)

### 3. 编辑模式

完全心无旁骛的方式编辑文字：点击 **编辑工具栏** 最右侧的拉伸按钮或者按下 `Ctrl + M`，将 Cmd Markdown 切换到独立的编辑模式，这是一个极度简洁的写作环境，所有可能会引起分心的元素都已经被挪除，超清爽！

### 4. 实时的云端文稿

为了保障数据安全，Cmd Markdown 会将您每一次击键的内容保存至云端，同时在 **编辑工具栏** 的最右侧提示 `已保存` 的字样。无需担心浏览器崩溃，机器掉电或者地震，海啸——在编辑的过程中随时关闭浏览器或者机器，下一次回到 Cmd Markdown 的时候继续写作。

### 5. 离线模式

在网络环境不稳定的情况下记录文字一样很安全！在您写作的时候，如果电脑突然失去网络连接，Cmd Markdown 会智能切换至离线模式，将您后续键入的文字保存在本地，直到网络恢复再将他们传送至云端，即使在网络恢复前关闭浏览器或者电脑，一样没有问题，等到下次开启 Cmd Markdown 的时候，她会提醒您将离线保存的文字传送至云端。简而言之，我们尽最大的努力保障您文字的安全。

### 6. 管理工具栏

为了便于管理您的文稿，在 **预览区** 的顶部放置了如下所示的 **管理工具栏**：

![tool-manager](https://www.zybuluo.com/static/img/toolbar-manager.jpg)

通过管理工具栏可以：

<i class="icon-share"></i> 发布：将当前的文稿生成固定链接，在网络上发布，分享
<i class="icon-file"></i> 新建：开始撰写一篇新的文稿
<i class="icon-trash"></i> 删除：删除当前的文稿
<i class="icon-cloud"></i> 导出：将当前的文稿转化为 Markdown 文本或者 Html 格式，并导出到本地
<i class="icon-reorder"></i> 列表：所有新增和过往的文稿都可以在这里查看、操作
<i class="icon-pencil"></i> 模式：切换 普通/Vim/Emacs 编辑模式

### 7. 阅读工具栏

![tool-manager](https://www.zybuluo.com/static/img/toolbar-reader.jpg)

通过 **预览区** 右上角的 **阅读工具栏**，可以查看当前文稿的目录并增强阅读体验。

工具栏上的五个图标依次为：

<i class="icon-list"></i> 目录：快速导航当前文稿的目录结构以跳转到感兴趣的段落
<i class="icon-chevron-sign-left"></i> 视图：互换左边编辑区和右边预览区的位置
<i class="icon-adjust"></i> 主题：内置了黑白两种模式的主题，试试 **黑色主题**，超炫！
<i class="icon-desktop"></i> 阅读：心无旁骛的阅读模式提供超一流的阅读体验
<i class="icon-fullscreen"></i> 全屏：简洁，简洁，再简洁，一个完全沉浸式的写作和阅读环境

### 8. 阅读模式

在 **阅读工具栏** 点击 <i class="icon-desktop"></i> 或者按下 `Ctrl+Alt+M` 随即进入独立的阅读模式界面，我们在版面渲染上的每一个细节：字体，字号，行间距，前背景色都倾注了大量的时间，努力提升阅读的体验和品质。

### 9. 标签、分类和搜索

在编辑区任意行首位置输入以下格式的文字可以标签当前文档：

标签： 未分类

标签以后的文稿在【文件列表】（Ctrl+Alt+F）里会按照标签分类，用户可以同时使用键盘或者鼠标浏览查看，或者在【文件列表】的搜索文本框内搜索标题关键字过滤文稿，如下图所示：

![file-list](https://www.zybuluo.com/static/img/file-list.png)

### 10. 文稿发布和分享

在您使用 Cmd Markdown 记录，创作，整理，阅读文稿的同时，我们不仅希望它是一个有力的工具，更希望您的思想和知识通过这个平台，连同优质的阅读体验，将他们分享给有相同志趣的人，进而鼓励更多的人来到这里记录分享他们的思想和知识，尝试点击 <i class="icon-share"></i> (Ctrl+Alt+P) 发布这份文档给好友吧！

------

再一次感谢您花费时间阅读这份欢迎稿，点击 <i class="icon-file"></i> (Ctrl+Alt+N) 开始撰写新的文稿吧！祝您在这里记录、阅读、分享愉快！

作者 [@ghosert][3]     
2016 年 07月 07日    

[^LaTeX]: 支持 **LaTeX** 编辑显示支持，例如：$\sum_{i=1}^n a_i=0$， 访问 [MathJax][4] 参考更多使用方法。

[^code]: 代码高亮功能支持包括 Java, Python, JavaScript 在内的，**四十一**种主流编程语言。

[1]: https://www.zybuluo.com/mdeditor?url=https://www.zybuluo.com/static/editor/md-help.markdown
[2]: https://www.zybuluo.com/mdeditor?url=https://www.zybuluo.com/static/editor/md-help.markdown#cmd-markdown-高阶语法手册
[3]: http://weibo.com/ghosert
[4]: http://meta.math.stackexchange.com/questions/5020/mathjax-basic-tutorial-and-quick-reference

