Imports System.IO
Imports System.Text

Module crypto
    Dim extractPath = ""

    Private Sub decryptFile(ByRef arc As BinaryReader, ByRef fileLoc As String, ByVal size As Integer, ByVal key As Integer)
        Dim data As Byte() = arc.ReadBytes(size)

        If Directory.Exists(Path.GetDirectoryName(fileLoc)) = False Then
            Directory.CreateDirectory(Path.GetDirectoryName(fileLoc))
        End If

        DeleteFileIfExist(fileLoc)

        Dim wr As New BinaryWriter(File.OpenWrite(fileLoc))
        Dim decData(data.Length - 1) As Byte
        Dim keyB As Byte() = BitConverter.GetBytes(key)
        Dim j As Integer = 0
        For i As Integer = 0 To data.Length - 1
            If (i <> 0 And i Mod 4 = 0) Then
                key = key * 7 + 3
                keyB = BitConverter.GetBytes(key)
            End If
            decData(i) = data(i) Xor keyB(i Mod 4)
        Next i
        wr.Write(decData)
        wr.Close()

    End Sub

    Private Function decryptV3(arc As BinaryReader)
        Dim pad As Integer = arc.ReadInt32() * 9 + 3

        Do
            Dim offs As Integer = arc.ReadInt32() Xor pad
            Dim size As Integer = arc.ReadInt32() Xor pad
            Dim key As Integer = arc.ReadInt32() Xor pad
            Dim length As Integer = arc.ReadInt32() Xor pad

            If offs = 0 Then
                arc.Close()
                patchTool.ToolStripStatusLabel.Text = "Finished extracting archive"
                Return True
            Else
                Dim name As String
                Dim nameEnc As Byte() = arc.ReadBytes(length)
                Dim nameDec(nameEnc.Length - 1) As Byte

                Dim padBytes As Byte() = BitConverter.GetBytes(pad)
                For i As Integer = 0 To nameEnc.Length - 1
                    nameDec(i) = nameEnc(i) Xor padBytes(i Mod 4)
                Next i

                name = Encoding.UTF8.GetString(nameDec)
                patchTool.ToolStripStatusLabel.Text = "Extracting: " + name

                Dim pos As Long = arc.BaseStream.Position
                arc.BaseStream.Seek(offs, SeekOrigin.Begin)
                decryptFile(arc, extractPath + name, size, key)

                arc.BaseStream.Seek(pos, SeekOrigin.Begin)
                Dim prog = (offs + size) / arc.BaseStream.Length
                patchTool.ProgressBarStep.Value = 100 * prog

                Application.DoEvents()
            End If
        Loop
    End Function

    Public Sub doExtract(ByVal basePath As String)
        extractPath = basePath + "\"
        Dim result As SByte = -1
        Try
            Dim br As New BinaryReader(File.OpenRead(basePath + "/Game.rgss3a"))
 
            If ReadCString(br, 7) = "RGSSAD" Then
                result = br.ReadSByte()
            End If

            Select Case result
                Case 1
                    ' &HDEADCAFE
                    Exit Select
                Case 3
                    decryptV3(br)
                    Exit Select
                Case -1
                    MsgBox("Error opening file")
                    Exit Select
                Case Else
                    MsgBox("Unknown RPGMaker Version")
                    Exit Select
            End Select
            br.Close()
        Catch ex As Exception
            MsgBox("Directory does not appear to contain a V3 encrypted rpgmaker game." + Environment.NewLine + ex.Message)
        End Try

    End Sub

End Module