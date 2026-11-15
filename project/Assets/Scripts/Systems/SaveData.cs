using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Cysharp.Threading.Tasks;

/// <summary>key/value를 보관하고 XML 직렬화·암호화·파일 기록·백업 복구를 담당</summary>
public class SaveData
{
    public static string CommonPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Project_Chrono"); // 모든 슬롯을 저장할 사용자 문서 폴더 아래의 공통 경로
    public Dictionary<string, object> Values = new Dictionary<string, object>(); // 호출자가 key로 조회·변경할 슬롯의 메모리 저장값을 보관
    public string FileName; // 현재 슬롯의 sav 파일명이며 저장 실패 안내에도 사용
    public string FilePath; // 현재 슬롯의 sav 파일을 읽고 쓸 경로
    public string BackupPath; // sav 손상 시 복구에 사용할 슬롯의 bak 파일 경로
    public string LastError; // 파일 로드 과정의 마지막 실패 이유를 SaveManager에 전달

    private const int MaximumFileSize = 1024 * 1024; // 손상 파일을 읽거나 과도한 데이터를 기록하지 않도록 제한할 최대 바이트 수
    private static readonly byte[] EncryptionKey = GetKey("ProjectChrono"); // ProjectChrono에서 만든 암복호화·무결성 검사 공용 키
    private static XmlSerializer Serializer = new XmlSerializer(typeof(Field[]), new[] { typeof(List<string>) }); // key/value와 문자열 목록을 XML로 변환할 공용 직렬화기

    /// <summary>SaveManager가 지정한 슬롯의 파일 경로를 준비</summary>
    public SaveData(string directory, int slot)
    {
        FileName = $"slot{slot + 1}.sav";
        FilePath = Path.Combine(directory, FileName);
        BackupPath = Path.ChangeExtension(FilePath, ".bak");
    }

    /// <summary>호출자가 알고 있는 key의 값을 요청한 타입으로 반환</summary>
    public T Get<T>(string key)
    {
        return (T)Values[key];
    }

    /// <summary>호출자가 지정한 key의 메모리 값만 추가·갱신</summary>
    public void Set<T>(string key, T value)
    {
        Values[key] = value;
    }

    /// <summary>저장 요청 당시의 값과 문자열 목록을 복사하여 이후 게임 변경과 분리</summary>
    public SaveData Copy()
    {
        var copy = (SaveData)MemberwiseClone();
        copy.Values = new Dictionary<string, object>();
        foreach (var pair in Values)
            copy.Values[pair.Key] = pair.Value is List<string> list ? new List<string>(list) : pair.Value;
        return copy;
    }

    /// <summary>Title 준비 시 sav·bak 순서로 읽고 모두 실패하면 빈 슬롯으로 처리</summary>
    public async UniTask<bool> LoadAsync()
    {
        // 작업 스레드에서 원본을 먼저 읽고 실패하면 백업 복구 시도
        Dictionary<string, object> loaded = null;
        string error = null;
        await UniTask.RunOnThreadPool(() =>
        {
            foreach (string path in new[] { FilePath, BackupPath })
            {
                if (!File.Exists(path))
                    continue;
                try
                {
                    loaded = ReadFile(path);
                    break;
                }
                catch (Exception exception) when (IsFileError(exception))
                {
                    error = $"{Path.GetFileName(path)}: {exception.Message}";
                }
            }
        });

        // 읽기에 성공한 값만 적용하고 실패 이유는 슬롯 안내에 전달
        LastError = error;
        if (loaded == null)
            return false;
        Values = loaded;
        return true;
    }

    /// <summary>독립 복사본을 기록·검사하며 sav 실패는 한 번 재시도한 뒤 기존 백업으로 복구</summary>
    public async UniTask SaveAsync()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            // SaveManager의 독립 복사본을 직렬화하고 원본 기록을 최대 두 번 시도
            byte[] bytes = Encode();
            Exception failure = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    WriteValidatedFile(FilePath, bytes);
                    failure = null;
                    break;
                }
                catch (Exception exception) when (IsFileError(exception))
                {
                    failure = exception;
                }
            }

            // 재시도까지 실패하면 이전 백업 복원 후 실패 사유 전달
            if (failure != null)
            {
                string recovery;
                try
                {
                    RestoreBackup();
                    recovery = "기존 백업 복원 완료";
                }
                catch (Exception exception) when (IsFileError(exception))
                {
                    recovery = $"백업 복원 실패: {exception.Message}";
                }
                throw new IOException($"{FileName} 저장 및 재시도 실패: {failure.Message}; {recovery}", failure);
            }

            // 검증된 sav만 백업으로 승격하며 이전 bak는 교체 직전까지 보존
            WriteValidatedFile(BackupPath, File.ReadAllBytes(FilePath));
        });
    }

    /// <summary>주어진 파일의 인증값과 XML 복원 가능 여부를 검사</summary>
    public bool ValidateFile(string path)
    {
        try
        {
            ReadFile(path);
            return true;
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            return false;
        }
    }

    /// <summary>SaveAsync의 재시도 실패 시 유효한 bak를 검사한 후 sav로 복원</summary>
    public void RestoreBackup()
    {
        ReadFile(BackupPath);
        WriteValidatedFile(FilePath, File.ReadAllBytes(BackupPath));
    }

    /// <summary>세피리아처럼 key/value 목록을 XML로 직렬화한 뒤 암호화·인증값을 붙</summary>
    private byte[] Encode()
    {
        // 메모리의 key/value를 XML 항목 목록으로 직렬화
        var fields = new List<Field>();
        foreach (var pair in Values)
            fields.Add(new Field { Key = pair.Key, Value = pair.Value });
        using var payload = new MemoryStream();
        Serializer.Serialize(payload, fields.ToArray());

        // 임의 IV를 붙여 XML 데이터를 AES로 암호화
        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        byte[] plain = payload.ToArray();
        byte[] encrypted = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        byte[] authenticated = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, authenticated, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, authenticated, aes.IV.Length, encrypted.Length);

        // IV와 암호문 전체의 무결성 인증값을 파일 앞부분에 추가
        using var hmac = new HMACSHA256(EncryptionKey);
        byte[] signature = hmac.ComputeHash(authenticated);
        byte[] result = new byte[signature.Length + authenticated.Length];
        Buffer.BlockCopy(signature, 0, result, 0, signature.Length);
        Buffer.BlockCopy(authenticated, 0, result, signature.Length, authenticated.Length);

        // 허용 용량을 넘는 저장값은 파일 기록 전에 거부
        if (result.Length > MaximumFileSize)
            throw new InvalidDataException("저장 파일 크기 한도를 초과함");
        return result;
    }

    /// <summary>용량·인증값을 검사하고 XML의 key/value 목록을 Dictionary로 복원</summary>
    private Dictionary<string, object> ReadFile(string path)
    {
        // 비정상 용량과 인증값 불일치를 복호화 전에 차단
        long size = new FileInfo(path).Length;
        if (size < 64 || size > MaximumFileSize)
            throw new InvalidDataException("저장 파일 크기가 유효하지 않음");
        byte[] bytes = File.ReadAllBytes(path);
        using var hmac = new HMACSHA256(EncryptionKey);
        byte[] expected = hmac.ComputeHash(bytes, 32, bytes.Length - 32);
        int difference = 0;
        for (int index = 0; index < expected.Length; index++)
            difference |= expected[index] ^ bytes[index];
        if (difference != 0)
            throw new InvalidDataException("저장 파일 무결성 검사 실패");

        // 파일에 저장된 IV와 공용 키로 암호문 복호화
        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        byte[] iv = new byte[16];
        Buffer.BlockCopy(bytes, 32, iv, 0, iv.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        byte[] plain = decryptor.TransformFinalBlock(bytes, 48, bytes.Length - 48);

        // 외부 XML 참조를 금지하고 원래 key/value 사전 복원
        using var stream = new MemoryStream(plain);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var fields = (Field[])Serializer.Deserialize(reader);
        var values = new Dictionary<string, object>();
        foreach (Field field in fields)
            values.Add(field.Key, field.Value);
        return values;
    }

    /// <summary>임시 파일을 검증한 후 교체하여 기존 sav·bak의 중간 손상을 방지</summary>
    private void WriteValidatedFile(string path, byte[] bytes)
    {
        string temporaryPath = path + ".tmp";
        try
        {
            // 대상 파일을 유지한 채 임시 파일에 기록·디스크 반영
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            // 임시 파일 검증 후 원본 교체와 최종 파일 재검사
            ReadFile(temporaryPath);
            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);
            ReadFile(path);
        }
        finally
        {
            // 실패 중 남은 임시 파일까지 정리
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>고정 문자열 ProjectChrono를 AES에 필요한 32바이트 키로 변환하여 암복호화·무결성 검사에 함께 사용</summary>
    private static byte[] GetKey(string value)
    {
        using var hash = SHA256.Create();
        return hash.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>파일 손상·접근 실패에 해당하는 예외만 복구 대상으로 분류</summary>
    private static bool IsFileError(Exception exception)
    {
        return exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
            exception is CryptographicException || exception is ArgumentException ||
            exception is InvalidOperationException || exception is XmlException;
    }

    /// <summary>XML에서 한 저장 항목의 key와 실제 타입을 가진 값을 표현</summary>
    public class Field
    {
        public string Key; // XML에 기록할 저장 항목의 조회 식별자
        public object Value; // XML에 실제 타입과 함께 기록할 저장 항목의 값
    }
}
