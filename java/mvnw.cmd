@REM ----------------------------------------------------------------------------
@REM Apache Maven Wrapper startup batch script
@REM ----------------------------------------------------------------------------
@IF "%__MVNW_ARG0_NAME__%"=="" (SET "__MVNW_ARG0_NAME__=%~nx0")
@SET ___MVNW_UPDATED_MAVEN_WRAPPER=
@SET __MVNW_LAUNCHER_JAR=%~dp0.mvn\wrapper\maven-wrapper.jar
@IF NOT EXIST "%__MVNW_LAUNCHER_JAR%" (
  SET ___MVNW_UPDATED_MAVEN_WRAPPER=TRUE
  @CALL :download_wrapper
)
@IF ERRORLEVEL 1 GOTO error

@SET MAVEN_JAVA_EXE=%JAVA_HOME%\bin\java.exe
@IF NOT "%JAVA_HOME%"=="" GOTO execute
@SET MAVEN_JAVA_EXE=java.exe
:execute
@SET WRAPPER_LAUNCHER=org.apache.maven.wrapper.MavenWrapperMain
@SET DOWNLOAD_URL=https://repo.maven.apache.org/maven2/org/apache/maven/wrapper/maven-wrapper/3.3.2/maven-wrapper-3.3.2.jar
@%MAVEN_JAVA_EXE% %JVM_CONFIG_MAVEN_PROPS% %MAVEN_OPTS% %MAVEN_DEBUG_OPTS% -classpath "%__MVNW_LAUNCHER_JAR%" "-Dmaven.multiModuleProjectDirectory=%__MVNW_ARG0_NAME__%\.." %WRAPPER_LAUNCHER% %MAVEN_CONFIG% %*
@IF ERRORLEVEL 1 GOTO error
@GOTO end
:download_wrapper
  @echo Downloading Maven Wrapper...
  @powershell -Command "Invoke-WebRequest -Uri '%DOWNLOAD_URL%' -OutFile '%__MVNW_LAUNCHER_JAR%'" 2>nul
  @EXIT /B %ERRORLEVEL%
:error
  @EXIT /B %ERRORLEVEL%
:end
