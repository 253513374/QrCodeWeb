

# 一、QrCodeWeb 项目说明

本项目是使用opencvsharp4 开源库 搭建的二维码识别API 。

主要使用：opencvsharp4 中的微信二维码识别模块。

目前opencvsharp4 正式发布的4.5.3 中是没有微信二维码识别功能的，要使用微信二维码识别功能 。

但是当前opencvsharp4  在年初的时候合并了一个提交， 当中存在WeChatQRCode模块。

本人使用opencvsharp4  源码重新编译了一个版本，使得可以通过opencvsharp4  使用微信开源的二维码模块。

windows编译步骤如下

1、首选先合并编译opencv与opencv_contrib 得到完整版的opencv

2、使用编译好的opencv版本  来编译opencvsharp4  项目中的OpenCvSharpExtern项目

（编译完成之后得到OpenCvSharp.dll与OpenCvSharpExtern.dll）

3、在QrCodeWeb 项目中 添加引用OpenCvSharp.dll

**注意：因为OpenCvSharp.dll依赖OpenCvSharpExtern.dll，所以还要把OpenCvSharpExtern.dll文件放在项目运行目录（即：编译好的程序目录）**





linux 下的也是一样的步骤 ， 本人把linux 下的编译经历 也整理了以下，仅供参考。

# 二、linux 编译opncv（4.5.5）

## 安装cmake

官网下载https://cmake.org/download/

 下载指定版本 cmake-3.23.1-linux-x86_64.tar.gz



```bash
解压：
tar -zxvf cmake-3.23.1-linux-x86_64.tar.gz
切换到bin目录
cd cmake-3.23.1-linux-x86_64.tar.gz/bin
```



cp /productdata/qrcodedecoding/cmake/bin/cmake  /usr/bin/



**## 检查**



## 添加cmake 环境变量

```ini

#1、修改环境变量
 vi /etc/profile 编辑文件，写入export PATH=$PATH:$/productdata/qrcodedecoding/cmake/bin（安装路径）
#2、使修改生效  
 source /etc/profile 
#3、查看PATH值
 echo $PATH  
#4、添加软连接：
sudo ln -s cmake   /usr/bin/cmake
或者sudo ln -sf ./cmake  /usr/bin/cmake
```



## opencv 与扩展模块联合编译

### 1、下载opencv源码

https://github.com/opencv/opencv 

<img src="C:\Users\q4528\AppData\Roaming\Typora\typora-user-images\image-20220507135107324.png" alt="image-20220507135107324" style="zoom: 33%;" />



### 2、下载opencv的扩展opencv_contrib

https://github.com/opencv/opencv_contrib

<img src="C:\Users\q4528\AppData\Roaming\Typora\typora-user-images\image-20220507135256069.png" alt="image-20220507135256069" style="zoom: 33%;" />

 

**注意：opencv与opencv_contrib 的源码版本最好一致，避免联合编译过程中出现一些莫名的错误**





cd /productdata/qrcodedecoding/opencv/opencv-4.5.5/

cd  /productdata/qrcodedecoding/cmake/bin/

/productdata/qrcodedecoding/cmake-3.23.1-linux-x86_64/share/

## 构建opencv项目进行编译

1、使用源码[opencv+ opencv_contrib]带有扩展模块的opencv
使用前面安装好的cmake来构建

2、命令模板

```
 cd <opencv_build_directory>
 cmake -DOPENCV_EXTRA_MODULES_PATH=<opencv_contrib>/modules <opencv_source_directory>

```

3、按照模板填入编译参数，进行源码构建（编译参数有很多，感兴趣的可查找资料自行查看各个参数的说明）

```
 cmake -DOPENCV_EXTRA_MODULES_PATH=/productdata/qrcodedecoding/opencv/opencv_contrib-4.5.5/modules /productdata/qrcodedecoding/opencv/opencv-4.5.5/
```


2、以下命令 编译构建好的opencv 源码
make -j5 

3、安装编译好的opencv库 

sudo make install

## 把编译好的opencv配置到系统环境变量

1、添加库路径---创建opencv.conf配置文件

添加文件：vi /etc/ld.so.conf.d/opencv.conf

输入：/usr/local/lib       ('o'键进入编辑模式或者‘ESC’键 进入/退出编辑模式 )

保存文件并退出：确保已经退出编辑模式之后，按   ‘SHIFT’+‘：’  输入	wq(w 保存，q 退出)  保存退出。

2、添加系统环境变量

打开环境变量文件：vi /etc/profile

末尾输入环境变量： 

export PKG_CONFIG_PATH=$PKG_CONFIG_PATH:/usr/local/lib/pkgconfig

export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:/usr/local/lib

保存文件并退出：确保已经退出编辑模式之后，按   ‘SHIFT’+‘：’  输入	wq(w 保存，q 退出)  保存退出。

更新环境变量：source /etc/profile

查看环境变量：echo $PKG_CONFIG_PATH（path 是环境变量名称）

刷新系统缓存：ldconfig

查看opencv是否安装成功：

输入命令：pkg-config-cflags opencv

​         pkg-config-libs opencv  

如果没有出错，说明安装成功。



## 编译opencvsharp 

下载opencvsharp 4 源码，cd 转到目录[OpenCvSharpExtern]。

1、 使用cmake . 可以先检查文件（可选），

2、使用make 命令 开始编译文件，

3、编译完成：生成libOpenCvSharpExtern.so文件

4、拷贝libOpenCvSharpExtern.so文件到 net 程序目录



## 部署netcore 程序

### 1、安装运行时

本次使用的是linux centos 7服务器，centos 7系统采用的是 yum 安装管理器

在安装.NET之前，我们需要先注册Microsoft密钥和源，在终端里面执行下面的命令：

```
sudo rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm
```

安装可更新的组产品或组件

```
sudo yum update
```

最后安装ASP.NET Core 运行时，版本自己旋转

```
sudo yum install aspnetcore-runtime-3.1 
```

离线安装方式：

官网下载需要的net for linux 安装包，然后解压到指定目录。

```
tar zxf aspnetcore-runtime-3.1.1-linux-x64.tar.gz -C /var/lib/dotnet
```

设置环境变量 

```
export DOTNET_ROOT=/var/lib/dotnet
export PATH=$PATH:/var/lib/dotnet
```

注意：这种设置环境变量的方式只对当前会话窗口起作用，在另外的会话窗口就不起作用了

dotnet --info  可查看信息，正确配置可返回正确详细信息。

为了解决这个问题，我们需要创建软链接方式来设置环境变量。

```
ln -s /var/lib/dotnet/dotnet /usr/local/bin
```

设置好了之后所有会话窗口访问了。

部署程序

使用ftp 上传发布好的程序文件，解压到指定文件目录

### 2、使用nginx代理

### 3、使用kestrel部署

程序启动命令：dotnet  QrCodeWeb.dll  --urls http://*:5000

[swagger ui](http://10.253.100.14:5000/swagger/index.html)

#### 创建常驻服务







## 守护进程

## 安装Supervisor

采用命令安装：    yum install supervisor

卸载 yum -y remove supervisor      

Supervisor项目配置文件

查看编辑主配置文件：vi  /etc/supervisor/supervisord.conf

程序配置文件 vi  /etc/supervisord.d/qr.ini



```ini
[program:QrCodeWeb]                          ;自定义进程名称, 根据自己喜好命名
command=dotnet QrCodeWeb.dll             ;程序启动命令 使用dotnet 命令
directory=/productdata/qrcodedecoding/publish                            ;命令执行的目录 你.NET Core 程序存放目录
autostart=true                                ;在Supervisord启动时，程序是否启动
autorestart=true                              ;程序退出后自动重启
startretries=5                                ;启动失败自动重试次数，默认是3
startsecs=1                                   ;自动重启间隔
user=root                                     ;设置启动进程的用户，默认是root
priority=999                                  ;进程启动优先级，默认999，值小的优先启动
stderr_logfile=/productdata/qrcodedecoding/publish/AbpMPA.err.log        ;标准错误日志 路径可以自定义
stdout_logfile=/productdata/qrcodedecoding/publish/AbpMPA.out.log        ;标准输出日志  路径可以自定义
environment=ASPNETCORE_ENVIRONMENT=Production ;进程环境变量
stopsignal=INT                                ;请求停止时用来杀死程序的信号

```

取消链接 （删除）

```
sudo  unlink /var/run/supervisor/supervisor.sock
```

```
sudo unlink /tmp/supervisor.sock
```

创建文件 sudo  touch /var/run/supervisor/supervisor.sock 

查看服务状态 sudo systemctl status supervisord.service

查看版本 supervisord version

启动  supervisord -c /etc/supervisor/supervisord.conf



## 创建开机自动启动supervisord服务

首先，我们得先在系统中创建一个服务文件supervisord.service (名称自定义)

1、服务名称：supervisord.service 

2、所在目录路径： cd /usr/lib/systemd/system/

3、编辑： vi /usr/lib/systemd/system/supervisord.service  进入编辑模式（‘O’ 键） 写入以下配置

```ini
[Unit]
Description=Supervisor daemon

[Service]
Type=forking
#服务启动命令
ExecStart=/usr/bin/supervisord -c /etc/supervisor/supervisord.conf 
#服务停止
ExecStop=/usr/bin/supervisorctl $OPTIONS shutdown
#服务重新加载
ExecReload=/usr/bin/supervisorctl $OPTIONS reload
KillMode=process
Restart=on-failure
RestartSec=42s

[Install]
WantedBy=multi-user.target
```

设置服务自动启动

```ini
# 注册服务
sudo systemctl enable supervisord.service

systemctl daemon-reload
# 启动服务
sudo systemctl start  supervisord.service
# 停止服务
sudo systemctl stop  supervisord.service
```



查看站点是否正常运行（查看程序进程是否运行）：ps -aux | grep  "QrCodeWeb.dll"

错误：

Error: Another program is already listening on a port that one of our HTTP servers is configured to use.  Shut this program down first before starting supervisord.、

解决办法

卸载重装



