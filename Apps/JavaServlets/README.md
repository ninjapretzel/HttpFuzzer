# Java Servlets


## JDK

### Windows

https://www.oracle.com/java/technologies/javase-downloads.html 
Click on JDK Download
Download Windows x64 Installer
Install and wait for it to finish

Locate the path to the Java Development Kit
    Often in C:\Program Files\Java
Set path to the JDK and Maven
```
λ setx path "%path%; C:\Program Files\Java\jdk-10.0.2\bin"
```
Alternatively using "Control Panel\All Control Panel Items\System":  
- Click "Advanced system settings" on the left  
- Click "Environment variables" button near bottom
- Find "Path" in the top list and double click it
- Insert "C:\Program Files\Java\jdk-10.0.2\bin" or wherever the JDK applications were installed to.
- Click "OK" buttons until all popups are dismissed

### Mac
https://www.oracle.com/java/technologies/javase-downloads.html 
Click on JDK Download
Download macOS Installer
Install and wait for it to finish

Add JDK to your path 
*(I don't have a Mac)*

### Linux
https://www.oracle.com/java/technologies/javase-downloads.html 
Click on JDK Download
Download 
Install and wait for it to finish

Add JDK to your path or `.bashrc`

### Test
Open up a terminal/command prompt and run some commands to make sure java is on the path
```
λ javac --version
javac 11.0.4
λ java --version
openjdk 11.0.4 2019-07-16
OpenJDK Runtime Environment AdoptOpenJDK (build 11.0.4+11)
OpenJDK 64-Bit Server VM AdoptOpenJDK (build 11.0.4+11, mixed mode)
```

If you get different output, that is fine.

## Maven
### All Platforms:
[Download Maven Here](https://maven.apache.org/download.cgi)  
[They also have an install guide here](https://maven.apache.org/install.html)

If their install guide is hard to parse:
- Download "Binary Zip Archive" (eg `apache-maven-3.6.3-bin.zip`)
- Extract it wherever you want it
- Add the extracted /bin/ path to your PATH

### Windows: 
```
λ setx path "%path%; C:\whatever\programs\apache-maven-3.6.3\bin"
```
Alternatively using "Control Panel\All Control Panel Items\System":  
- Click "Advanced system settings" on the left  
- Click "Environment variables" button near bottom
- Find "Path" in the top list and double click it
- Insert the `/bin/` folder of wherever you extracted the Maven application to.
- Click "OK" buttons until all popups are dismissed

### Test
Run a command to test that maven is on the path:
```
λ mvn --version
Apache Maven 3.6.3 (cecedd343002696d0abb50b32b541b8a6ba2883f)
Maven home: D:\NON-OS\programs\apache-maven-3.6.3\bin\..
Java version: 1.8.0_222, vendor: AdoptOpenJDK, runtime: C:\Program Files\AdoptOpenJDK\jdk-8.0.222.10-hotspot\jre

Default locale: en_US, platform encoding: Cp1252
OS name: "windows 10", version: "10.0", arch: "amd64", family: "windows"splay/MAVEN/NoGoalSpecifiedException
```