Imports System.IO
Imports System.Text

Module helpers
    Public thisExe As String = New Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath
    Dim tmpExe As String = ""
    Dim progress As Integer
    Dim wasHer As String = "495_UN_OWEN_495"

    Public Function PatchStart()
        Dim br As New BinaryReader(File.OpenRead(thisExe))
        Dim backOffs = br.BaseStream.Length - wasHer.Length
        br.BaseStream.Seek(backOffs, SeekOrigin.Begin)
        Dim cur As String = System.Text.Encoding.UTF8.GetString(br.ReadBytes(wasHer.Length))

        If (cur = wasHer) Then
            br.BaseStream.Seek(backOffs - 4, SeekOrigin.Begin)
            Return BitConverter.ToInt32(br.ReadBytes(4), 0)
        End If
        Return 0
    End Function

    Public Function OpenFileDlg(ByRef location As String) As Boolean
        Dim openDir As New FolderBrowserDialog

        openDir.RootFolder = Environment.SpecialFolder.Desktop
        openDir.SelectedPath = Application.StartupPath

        If (location.Length > 0) Then
            openDir.SelectedPath = location
        End If
        openDir.Description = "Choose the directory of the game"

        If openDir.ShowDialog() <> Windows.Forms.DialogResult.OK Then
            Return False
        End If
        location = openDir.SelectedPath
        Return True
    End Function

    Public Function DeleteFileIfExist(ByVal sPath As String) As Integer
        Try
            If File.Exists(sPath) Then
                File.Delete(sPath)
            End If
            Return 0
        Catch ex As Exception
            MsgBox(ex.ToString)
        End Try
        Return -1
    End Function



    Public Function MakePatch(origDir As String, patchDir As String)
        tmpExe = patchTool.TextOut.Text + patchTool.patchName.Text + ".exe"
        DeleteFileIfExist(tmpExe)
        patchTool.ToolStripStatusLabel.Text = "Calculating number of files"
        Dim numVals = DirSearch(patchDir, patchDir, origDir, 0)
        progress = 0
        My.Computer.FileSystem.CopyFile(thisExe, tmpExe)
        Dim infoReader As System.IO.FileInfo
        infoReader = My.Computer.FileSystem.GetFileInfo(tmpExe)
        Dim fileOffset As Integer = infoReader.Length
        DirSearch(patchDir, patchDir, origDir, numVals)
        My.Computer.FileSystem.WriteAllBytes(tmpExe, BitConverter.GetBytes(fileOffset), True)
        My.Computer.FileSystem.WriteAllText(tmpExe, wasHer, True)
        patchTool.ProgressBarStep.Value = 100
        patchTool.ToolStripStatusLabel.Text = "Patch created: "
    End Function

    Private Function ReservedFileName(f As String)
        Dim reserved As String() = {"\Game.rgss3a.extracted", "\Game", "\Game.rgss3a"}
        For Each str As String In reserved
            If f.Equals(str) Or f.StartsWith("\Save\") Then
                Return True
            End If
        Next
        Return False
    End Function

    Public Function ApplyPatch(initialOffs As Integer)
        Try
            ' Check if encrypted game
            If My.Computer.FileSystem.FileExists("./Game.rgss3a") Then
                doExtract("./")
                My.Computer.FileSystem.MoveFile("./Game.rgss3a", "./Game.rgss3a.extracted")
            End If
            
            ' Now extract the archive from the exe
            Dim br As New BinaryReader(File.OpenRead(thisExe))
            Dim fileLen As Long = br.BaseStream.Length - initialOffs
            Dim stopOffs As Long = br.BaseStream.Length - 4 - wasHer.Length
            br.BaseStream.Seek(initialOffs, SeekOrigin.Begin)

            Do Until br.BaseStream.Position >= br.BaseStream.Length - 4 - wasHer.Length
                Dim length As Integer = BitConverter.ToInt32(br.ReadBytes(4), 0)
                Dim nameSize As Integer = BitConverter.ToInt32(br.ReadBytes(4), 0)
                Dim name As String = System.Text.Encoding.UTF8.GetString(br.ReadBytes(nameSize))

                patchTool.ToolStripStatusLabel.Text = "Extracting " + name
                Dim offByOne = br.ReadByte
                Dim writeLoc = ".\" + name
                If Directory.Exists(Path.GetDirectoryName(writeLoc)) = False Then
                    Directory.CreateDirectory(Path.GetDirectoryName(writeLoc))
                End If

                DeleteFileIfExist(writeLoc)
                Dim wr As New BinaryWriter(File.OpenWrite(writeLoc))
                wr.Write(br.ReadBytes(length))
                wr.Close()
                patchTool.ProgressBarStep.Value = 100 * (br.BaseStream.Position - initialOffs) / fileLen
                Application.DoEvents()
            Loop
            br.Close()
            patchTool.ToolStripStatusLabel.Text = "Patch applied successfully"
        Catch e As Exception
            patchTool.ToolStripStatusLabel.Text = "Failed to apply patch"
            Application.DoEvents()
            MessageBox.Show(e.ToString)
        End Try

    End Function

    Function DirSearch(ByVal sDir As String, ByVal patchDir As String, ByVal origDir As String, ByVal fileCount As Integer)
        Dim d As String
        Dim f As String
        Dim ret As Integer
        Try
            For Each f In Directory.GetFiles(sDir, "*")
                Dim lenDiff = f.Length - patchDir.Length
                Dim fileName As String = f.Substring(patchDir.Length, lenDiff)
                If (ReservedFileName(fileName)) Then
                    Continue For
                End If
                If fileCount = 0 Then
                    ret += 1
                Else
                    patchTool.ToolStripStatusLabel.Text = "Comparing: " + fileName
                    Application.DoEvents()
                    Dim bytes As Byte()

                    If (False = CompareFiles(f, origDir + fileName)) Then
                        patchTool.ToolStripStatusLabel.Text = "Archiving: " + fileName
                        bytes = System.Text.Encoding.UTF8.GetBytes(fileName)
                        Dim allBytes = File.ReadAllBytes(f)
                        Dim statArray As Byte() = New Byte(8 + bytes.Length) {}
                        Dim lenArray As Byte() = BitConverter.GetBytes(allBytes.Length)
                        Dim fnameArray As Byte() = BitConverter.GetBytes(bytes.Length)
                        Array.Copy(lenArray, 0, statArray, 0, 4)
                        Array.Copy(fnameArray, 0, statArray, 4, 4)
                        Array.Copy(bytes, 0, statArray, 8, bytes.Length)
                        My.Computer.FileSystem.WriteAllBytes(tmpExe, statArray, True)
                        My.Computer.FileSystem.WriteAllBytes(tmpExe, allBytes, True)
                    End If

                    ' Update progress bar
                    progress += 1
                    patchTool.ProgressBarStep.Value = 100 * progress / fileCount
                    Application.DoEvents()

                End If
            Next

            For Each d In Directory.GetDirectories(sDir)
                ret += DirSearch(d, patchDir, origDir, fileCount)
            Next
        Catch excpt As System.Exception
            Debug.WriteLine(excpt.Message)
        End Try
        Return ret
    End Function

    Public Function CompareFiles(origPath As String, patchPath As String)
        Dim ret = True
        Try
            Dim patchFile As New System.IO.StreamReader(patchPath)
            Dim origFile As New System.IO.StreamReader(origPath)
            Do Until patchFile.EndOfStream Or origFile.EndOfStream Or ret = False
                Dim patchChar = patchFile.Read
                Dim origChar = origFile.Read
                If origChar <> patchChar Then
                    ret = False
                End If
            Loop
            patchFile.Close()
            origFile.Close()
        Catch ex As Exception
            ret = False
        End Try
        Return ret
    End Function

    Public Function ReadCString(ByVal br As BinaryReader,
                                ByVal MaxLength As Integer,
                                Optional ByVal lOffset As Long = -1,
                                Optional ByVal enc As Encoding = Nothing) As String

        Dim fTemp As Long = br.BaseStream.Position
        Dim bTemp As Byte = 0, i As Integer = 0, result As String = ""

        If lOffset > -1 Then
            br.BaseStream.Seek(lOffset, SeekOrigin.Begin)
        End If

        Do
            bTemp = br.ReadByte()
            If bTemp = 0 Then Exit Do

            i += 1
        Loop While i < MaxLength

        If lOffset > -1 Then
            br.BaseStream.Seek(lOffset, SeekOrigin.Begin)
            If enc Is Nothing Then
                result = Encoding.ASCII.GetString(br.ReadBytes(i))
            Else
                result = enc.GetString(br.ReadBytes(i))
            End If
            br.BaseStream.Seek(fTemp, SeekOrigin.Begin)
        Else
            br.BaseStream.Seek(fTemp, SeekOrigin.Begin)
            If enc Is Nothing Then
                result = Encoding.ASCII.GetString(br.ReadBytes(i))
            Else
                result = enc.GetString(br.ReadBytes(i))
            End If
            br.BaseStream.Seek(fTemp + MaxLength, SeekOrigin.Begin)
        End If

        Return result
    End Function

End Module
