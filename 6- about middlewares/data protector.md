### 1. about





### 2. about encryptor



#### 2.1 encryptor algorithm configuration

```c#
internal interface IInternalAlgorithmConfiguration
{    
    // create descriptor from ISecret
    IAuthenticatedEncryptorDescriptor CreateDescriptorFromSecret(ISecret secret);    
    void Validate();
}

```

##### 2.1.1 algorithm configuration

```c#
public abstract class AlgorithmConfiguration
{
    internal const int KDK_SIZE_IN_BYTES = 512 / 8;
    
    // create new descriptor
    public abstract IAuthenticatedEncryptorDescriptor CreateNewDescriptor();
}

```

##### 2.1.2 authenticated encryptor configuration

```c#
public sealed class AuthenticatedEncryptorConfiguration : 
	AlgorithmConfiguration, 
	IInternalAlgorithmConfiguration
{    
    // (default) encryption algorithm
    public EncryptionAlgorithm EncryptionAlgorithm { get; set; } = EncryptionAlgorithm.AES_256_CBC;    
    // (default) validation algorithm
    public ValidationAlgorithm ValidationAlgorithm { get; set; } = ValidationAlgorithm.HMACSHA256;
       
    // 方法- create new descriptor，-> 转到 internal configuration 接口的 create 方法（传入 secret.random）  
    public override IAuthenticatedEncryptorDescriptor CreateNewDescriptor()
    {
        var internalConfiguration = (IInternalAlgorithmConfiguration)this;
        return internalConfiguration.CreateDescriptorFromSecret(Secret.Random(KDK_SIZE_IN_BYTES));
    }
    
    // 接口方法- create new descriptor   
    IAuthenticatedEncryptorDescriptor IInternalAlgorithmConfiguration.CreateDescriptorFromSecret(ISecret secret)
    {
        // 创建 authenticate encryptor descriptor
        return new AuthenticatedEncryptorDescriptor(this, secret);
    }
    
    // 方法- validate
    // 创建 encryptor factory => encryptor => 执行 perform self test
    void IInternalAlgorithmConfiguration.Validate()
    {        
        var factory = new AuthenticatedEncryptorFactory(NullLoggerFactory.Instance);
        // Run a sample payload through an encrypt 
        var encryptor = factory.CreateAuthenticatedEncryptorInstance(Secret.Random(512 / 8), this);
        try
        {
            encryptor.PerformSelfTest();
        }
        finally
        {
            (encryptor as IDisposable)?.Dispose();
        }
    }
}

```

###### 2.1.2.1 encryptor algorithm

```c#
public enum EncryptionAlgorithm
{    
    AES_128_CBC,        
    AES_192_CBC,    
    AES_256_CBC,  
    
    AES_128_GCM,        
    AES_192_GCM,        
    AES_256_GCM,
}

```

###### 2.1.2.2 validation algorithm

```c#
public enum ValidationAlgorithm
{    
    // The HMAC algorithm (RFC 2104) using the SHA-256 hash function (FIPS 180-4).    
    HMACSHA256,        
    // The HMAC algorithm (RFC 2104) using the SHA-512 hash function (FIPS 180-4).    
    HMACSHA512,
}

```

#### 2.2 authenticated encryptor descriptor

```c#
public interface IAuthenticatedEncryptorDescriptor
{    
    XmlSerializedDescriptorInfo ExportToXml();
}

```

##### 2.2.1 xml serialized descriptor info

```c#
public sealed class XmlSerializedDescriptorInfo
{
    // xml 数据（序列化以后）
    public XElement SerializedDescriptorElement { get; }
    // 逆序列化器类型
    public Type DeserializerType { get; }        
        
    public XmlSerializedDescriptorInfo(XElement serializedDescriptorElement, Type deserializerType)
    {
        if (serializedDescriptorElement == null)
        {
            throw new ArgumentNullException(nameof(serializedDescriptorElement));
        }        
        if (deserializerType == null)
        {
            throw new ArgumentNullException(nameof(deserializerType));
        }
        
        // 如果 deserializer type 没有实现 authenticated encryptor descriptor deserializer 接口，-> 抛出异常
        if (!typeof(IAuthenticatedEncryptorDescriptorDeserializer).IsAssignableFrom(deserializerType))
        {
            throw new ArgumentException(
                Resources.FormatTypeExtensions_BadCast(
                    deserializerType.FullName, 
                    typeof(IAuthenticatedEncryptorDescriptorDeserializer).FullName),
                    nameof(deserializerType));
        }
        
        // 注入 serialized element (xml)
        SerializedDescriptorElement = serializedDescriptorElement;
        // 注入 deserializer type
        DeserializerType = deserializerType;
    }    
}

```

##### 2.2.2 authenticated encryptor descriptor deserializer

```c#
// 接口
public interface IAuthenticatedEncryptorDescriptorDeserializer
{    
    IAuthenticatedEncryptorDescriptor ImportFromXml(XElement element);
}

// 实现
public sealed class AuthenticatedEncryptorDescriptorDeserializer : IAuthenticatedEncryptorDescriptorDeserializer
{    
    public IAuthenticatedEncryptorDescriptor ImportFromXml(XElement element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }
               
        // 创建 authenticated encryptor configuration（预结果）
        var configuration = new AuthenticatedEncryptorConfiguration();
        
        // 解析 xelement 的 "encryption" 节，解析为 encryption algorithm 并注入到 configuration（预结果）
        var encryptionElement = element.Element("encryption")!;
        configuration.EncryptionAlgorithm = (EncryptionAlgorithm)Enum.Parse(
            typeof(EncryptionAlgorithm), 
            (string)encryptionElement.Attribute("algorithm")!);
        
        // 如果 encryption algorithm 不是 gcm 算法
        if (!AuthenticatedEncryptorFactory.IsGcmAlgorithm(configuration.EncryptionAlgorithm))
        {
            // 解析 xelement 的 "validation" 节，解析为 validation algorithm 并注入到 configuration（预结果）
            var validationElement = element.Element("validation")!;
            configuration.ValidationAlgorithm = (ValidationAlgorithm)Enum.Parse(
                typeof(ValidationAlgorithm), 
                (string)validationElement.Attribute("algorithm")!);
        }
        
        // 解析 xelement 的 "masterKey"，转换为 secret
        Secret masterKey = ((string)element.Elements("masterKey").Single()).ToSecret();
        
        // 由 configuration、secret 创建 authenticated encryptor descriptor
        return new AuthenticatedEncryptorDescriptor(configuration, masterKey);
    }
}

```

##### 2.2.3 实现

* 创建 encryptor 的基本数据（元数据），必须包含一个 master key？

```c#
public sealed class AuthenticatedEncryptorDescriptor : IAuthenticatedEncryptorDescriptor
{
    // master key
    internal ISecret MasterKey { get; }    
    // encryptor configuration
    internal AuthenticatedEncryptorConfiguration Configuration { get; }
    
    public AuthenticatedEncryptorDescriptor(
        AuthenticatedEncryptorConfiguration configuration,
        ISecret masterKey)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }        
        if (masterKey == null)
        {
            throw new ArgumentNullException(nameof(masterKey));
        }
        
        // 注入 secret (master key)
        MasterKey = masterKey;
        // 注入 encryptor configuration
        Configuration = configuration;        
    }
                
    // 方法- export to xml
    // 创建 xml（包含 encryptor 算法、validation 算法、master key），
    // 然后由 xml 和 deserializer 封装为 xml descriptor info
    public XmlSerializedDescriptorInfo ExportToXml()
    {        
        // 创建 encryption 节
        var encryptionElement = new XElement(
            "encryption",
            new XAttribute("algorithm", Configuration.EncryptionAlgorithm));
        
        // 创建 validation element 节
        var validationElement = 
            (AuthenticatedEncryptorFactory.IsGcmAlgorithm(Configuration.EncryptionAlgorithm)) 
            	? (object)new XComment(
            		" AES-GCM includes a 128-bit authentication tag, no extra validation algorithm required. ")       
            	: (object)new XElement(
                     "validation",
                     new XAttribute("algorithm", Configuration.ValidationAlgorithm));
        
        // 创建 xelement，封装 encryption 节、validation 节、masterKey 
        var outerElement = new XElement(
            "descriptor",
            encryptionElement,
            validationElement,
            MasterKey.ToMasterKeyElement());
        
        // 创建 xml serialized descriptor info
        return new XmlSerializedDescriptorInfo(
            outerElement, 
            typeof(AuthenticatedEncryptorDescriptorDeserializer));
    }
}

```

###### 2.2.3.1 xml extensions

```c#
internal unsafe static class SecretExtensions
{
    public static XElement ToMasterKeyElement(this ISecret secret)
    {
        // Technically we'll be keeping the unprotected secret around in memory as a string, so it can get moved by the GC, 
        // but we should be good citizens and try to pin / clear our our temporary buffers regardless.
        byte[] unprotectedSecretRawBytes = new byte[secret.Length];
        string unprotectedSecretAsBase64String;
        fixed (byte* __unused__ = unprotectedSecretRawBytes)
        {
            try
            {
                secret.WriteSecretIntoBuffer(new ArraySegment<byte>(unprotectedSecretRawBytes));
                unprotectedSecretAsBase64String = Convert.ToBase64String(unprotectedSecretRawBytes);
            }
            finally
            {
                Array.Clear(unprotectedSecretRawBytes, 0, unprotectedSecretRawBytes.Length);
            }
        }
        
        var masterKeyElement = new XElement(
            "masterKey",
            new XComment(" Warning: the key below is in an unencrypted form. "),
            new XElement("value", unprotectedSecretAsBase64String));
        
        masterKeyElement.MarkAsRequiresEncryption();
        return masterKeyElement;
    }
        
    public static Secret ToSecret(this string base64String)
    {
        byte[] unprotectedSecret = Convert.FromBase64String(base64String);
        fixed (byte* __unused__ = unprotectedSecret)
        {
            try
            {
                return new Secret(unprotectedSecret);
            }
            finally
            {
                Array.Clear(unprotectedSecret, 0, unprotectedSecret.Length);
            }
        }
    }
}

```

###### 2.2.3.2 xelement extensions

```c#
public static class XmlExtensions
{
    internal static bool IsMarkedAsRequiringEncryption(this XElement element)
    {
        return ((bool?)element.Attribute(XmlConstants.RequiresEncryptionAttributeName)).GetValueOrDefault();
    }
        
    public static void MarkAsRequiresEncryption(this XElement element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }
        
        element.SetAttributeValue(XmlConstants.RequiresEncryptionAttributeName, true);
    }
}

```



#### 2.3 authenticated encryptor

```c#
public interface IAuthenticatedEncryptor
{    
    byte[] Decrypt(ArraySegment<byte> ciphertext, ArraySegment<byte> additionalAuthenticatedData);       
    byte[] Encrypt(ArraySegment<byte> plaintext, ArraySegment<byte> additionalAuthenticatedData);
}

```

##### 2.3.1 扩展接口- optimized 

```c#
// optimized auth encryptor, tamper-proof
internal interface IOptimizedAuthenticatedEncryptor : IAuthenticatedEncryptor
{    
    byte[] Encrypt(
        ArraySegment<byte> plaintext, 
        ArraySegment<byte> additionalAuthenticatedData, 
        uint preBufferSize, 
        uint postBufferSize);
}

```

##### 2.3.2 扩展方法

```c#
internal static class AuthenticatedEncryptorExtensions
{
    public static byte[] Encrypt(
        this IAuthenticatedEncryptor encryptor, 
        ArraySegment<byte> plaintext, 
        ArraySegment<byte> additionalAuthenticatedData, 
        uint preBufferSize, 
        uint postBufferSize)
    {
        // 如果 encryptor 实现了 optimized encryptor 接口，-> 调用 optimized encryptor 的 encrypt 方法
        var optimizedEncryptor = encryptor as IOptimizedAuthenticatedEncryptor;
        if (optimizedEncryptor != null)
        {
            return optimizedEncryptor.Encrypt(
                plaintext, 
                additionalAuthenticatedData, 
                preBufferSize, 
                postBufferSize);
        }
        
        // Fall back to the unoptimized version
        // 如果没有 pre & post，-> 调用 encryptor 的 encrypt 方法
        if (preBufferSize == 0 && postBufferSize == 0)
        {
            // optimization: call through to inner encryptor with no modifications
            return encryptor.Encrypt(plaintext, additionalAuthenticatedData);
        }
        // （否则，即有 pre & post，-> 加上 pre & post）
        else
        {
            var temp = encryptor.Encrypt(plaintext, additionalAuthenticatedData);
            var retVal = new byte[checked(preBufferSize + temp.Length + postBufferSize)];
            Buffer.BlockCopy(temp, 0, retVal, checked((int)preBufferSize), temp.Length);
            return retVal;
        }
    }
        
    // 静态方法- perform self test
    public static void PerformSelfTest(this IAuthenticatedEncryptor encryptor)
    {
        // Arrange
        var plaintextAsGuid = Guid.NewGuid();
        var plaintextAsBytes = plaintextAsGuid.ToByteArray();
        var aad = Guid.NewGuid().ToByteArray();
        
        // Act
        var protectedData = encryptor.Encrypt(new ArraySegment<byte>(plaintextAsBytes), new ArraySegment<byte>(aad));
        var roundTrippedData = encryptor.Decrypt(new ArraySegment<byte>(protectedData), new ArraySegment<byte>(aad));
        
        // Assert
        CryptoUtil.Assert(
            roundTrippedData != null && 
            roundTrippedData.Length == plaintextAsBytes.Length && 
            plaintextAsGuid == new Guid(roundTrippedData),
            "Plaintext did not round-trip properly through the authenticated encryptor.");
    }
}

```

#### 2.4 authenticated encryptor factory

```c#
public interface IAuthenticatedEncryptorFactory
{     
    // 由 key 创建 encryptor
    IAuthenticatedEncryptor? CreateEncryptorInstance(IKey key);
}

```

##### 2.4.1 实现 ( !universal! )

```c#
public sealed class AuthenticatedEncryptorFactory : IAuthenticatedEncryptorFactory
{
    private readonly ILoggerFactory _loggerFactory;
        
    public AuthenticatedEncryptorFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }
    
    // 方法- create encryptor
    public IAuthenticatedEncryptor? CreateEncryptorInstance(IKey key)
    {
        // a- key 的 encryptor descriptor 没有实现 encryptor descriptor 接口，-> 返回 null
        if (key.Descriptor is not AuthenticatedEncryptorDescriptor descriptor)
        {
            return null;
        }
        
        return CreateAuthenticatedEncryptorInstance(
            descriptor.MasterKey, 
            descriptor.Configuration);
    }
    
    [return: NotNullIfNotNull("authenticatedConfiguration")]
    internal IAuthenticatedEncryptor? CreateAuthenticatedEncryptorInstance(
        ISecret secret,
        AuthenticatedEncryptorConfiguration? authenticatedConfiguration)
    {
        // b- key 的 encrytor configuration 为 null，-> 返回 null
        if (authenticatedConfiguration == null)
        {
            return null;
        }
        
        // c- 如果 configuration 标记 encryption algorithm 是 gcm algorithm
        if (IsGcmAlgorithm(authenticatedConfiguration.EncryptionAlgorithm))
        {
#if NETCOREAPP
    		// c1- [netcore app]，-> 创建 aes gcm authenticated encryptor
    		return new AesGcmAuthenticatedEncryptor(
    			secret, 
    			// 2-
    			GetAlgorithmKeySizeInBits(authenticatedConfiguration.EncryptionAlgorithm) / 8);
#else
    		// [Not netcore app]，-> 创建 cng gcm authenticated encryptor（只能用在 windows 系统）
    		// GCM requires CNG, and CNG is only supported on Windows.
    
    		// 如果不是 windows 系统，-> 抛出异常
    		if (!OSVersionUtil.IsWindows())
            {
                throw new PlatformNotSupportedException(Resources.Platform_WindowsRequiredForGcm);
            }
            
            Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            
            // c2- 创建 cng gcm encryptor configuration
            var configuration = new CngGcmAuthenticatedEncryptorConfiguration()
            {
                // 1-
                EncryptionAlgorithm = 
                    GetBCryptAlgorithmNameFromEncryptionAlgorithm(authenticatedConfiguration.EncryptionAlgorithm),
                // 2- 
                EncryptionAlgorithmKeySize = 
                    GetAlgorithmKeySizeInBits(authenticatedConfiguration.EncryptionAlgorithm)
            };
            
            // 创建 cng gcm encryptor factory，并创建 encryptor
            return new CngGcmAuthenticatedEncryptorFactory(_loggerFactory)
                .CreateAuthenticatedEncryptorInstance(secret, configuration);
#endif
        }
        // d- configuration 的 encryption algorithm 不是 gcm algorithm
        else
        {
            // d1- 如果是 windows 系统，-> 创建 cng cbc encryptor
            if (OSVersionUtil.IsWindows())
            {
                Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
                // CNG preferred over managed implementations if running on Windows
                var configuration = new CngCbcAuthenticatedEncryptorConfiguration()
                {
                    // 1-
                    EncryptionAlgorithm = 
                        GetBCryptAlgorithmNameFromEncryptionAlgorithm(authenticatedConfiguration.EncryptionAlgorithm),
                    // 2-
                    EncryptionAlgorithmKeySize = 
                        GetAlgorithmKeySizeInBits(authenticatedConfiguration.EncryptionAlgorithm),
                    // 3-
                    HashAlgorithm = 
                        GetBCryptAlgorithmNameFromValidationAlgorithm(authenticatedConfiguration.ValidationAlgorithm)
                };
                
                return new CngCbcAuthenticatedEncryptorFactory(_loggerFactory)
                    .CreateAuthenticatedEncryptorInstance(secret, configuration);
            }
            // d2- 不是 windows 系统，-> 创建 managed encryptor
            else
            {
                // Use managed implementations as a fallback
                var configuration = new ManagedAuthenticatedEncryptorConfiguration()
                {
                    // 4-
                    EncryptionAlgorithmType = 
                        GetManagedTypeFromEncryptionAlgorithm(authenticatedConfiguration.EncryptionAlgorithm),
                    // 2-
                    EncryptionAlgorithmKeySize = 
                        GetAlgorithmKeySizeInBits(authenticatedConfiguration.EncryptionAlgorithm),
                    // 5-
                    ValidationAlgorithmType = 
                        GetManagedTypeFromValidationAlgorithm(authenticatedConfiguration.ValidationAlgorithm)
                };
                
                return new ManagedAuthenticatedEncryptorFactory(_loggerFactory)
                    .CreateAuthenticatedEncryptorInstance(secret, configuration);
            }
        }
    }
    
    internal static bool IsGcmAlgorithm(EncryptionAlgorithm algorithm)
    {
        return (EncryptionAlgorithm.AES_128_GCM <= algorithm && 
                algorithm <= EncryptionAlgorithm.AES_256_GCM);
    }
    
    // 2-
    private static int GetAlgorithmKeySizeInBits(EncryptionAlgorithm algorithm)
    {
        switch (algorithm)
        {
            case EncryptionAlgorithm.AES_128_CBC:
            case EncryptionAlgorithm.AES_128_GCM:
                return 128;
                
            case EncryptionAlgorithm.AES_192_CBC:
            case EncryptionAlgorithm.AES_192_GCM:
                return 192;
                
            case EncryptionAlgorithm.AES_256_CBC:
            case EncryptionAlgorithm.AES_256_GCM:
                return 256;
                
            default:
                throw new ArgumentOutOfRangeException(nameof(EncryptionAlgorithm));
        }
    }
    
    // 1-
    private static string GetBCryptAlgorithmNameFromEncryptionAlgorithm(EncryptionAlgorithm algorithm)
    {
        switch (algorithm)
        {
            case EncryptionAlgorithm.AES_128_CBC:
            case EncryptionAlgorithm.AES_192_CBC:
            case EncryptionAlgorithm.AES_256_CBC:
            case EncryptionAlgorithm.AES_128_GCM:
            case EncryptionAlgorithm.AES_192_GCM:
            case EncryptionAlgorithm.AES_256_GCM:
                return Constants.BCRYPT_AES_ALGORITHM;
                
            default:
                throw new ArgumentOutOfRangeException(nameof(EncryptionAlgorithm));
        }
    }
    
    // 3-
    private static string GetBCryptAlgorithmNameFromValidationAlgorithm(ValidationAlgorithm algorithm)
    {
        switch (algorithm)
        {
            case ValidationAlgorithm.HMACSHA256:
                return Constants.BCRYPT_SHA256_ALGORITHM;
                
            case ValidationAlgorithm.HMACSHA512:
                return Constants.BCRYPT_SHA512_ALGORITHM;
                
            default:
                throw new ArgumentOutOfRangeException(nameof(ValidationAlgorithm));
        }
    }
    
    // 4-
    private static Type GetManagedTypeFromEncryptionAlgorithm(EncryptionAlgorithm algorithm)
    {
        switch (algorithm)
        {
            case EncryptionAlgorithm.AES_128_CBC:
            case EncryptionAlgorithm.AES_192_CBC:
            case EncryptionAlgorithm.AES_256_CBC:
            case EncryptionAlgorithm.AES_128_GCM:
            case EncryptionAlgorithm.AES_192_GCM:
            case EncryptionAlgorithm.AES_256_GCM:
                return typeof(Aes);
                
            default:
                throw new ArgumentOutOfRangeException(nameof(EncryptionAlgorithm));
        }
    }
    
    // 5-
    private static Type GetManagedTypeFromValidationAlgorithm(ValidationAlgorithm algorithm)
    {
        switch (algorithm)
        {
            case ValidationAlgorithm.HMACSHA256:
                return typeof(HMACSHA256);
                
            case ValidationAlgorithm.HMACSHA512:
                return typeof(HMACSHA512);
                
            default:
                throw new ArgumentOutOfRangeException(nameof(ValidationAlgorithm));
        }
    }
}

```

#### 2.5-0 cng base

##### 2.5-0.1 cng encryptor base

```c#
internal unsafe abstract class CngAuthenticatedEncryptorBase : 
	IOptimizedAuthenticatedEncryptor, 
	IDisposable
{
    // 方法- 加密
    public byte[] Encrypt(
        ArraySegment<byte> plaintext, 
        ArraySegment<byte> additionalAuthenticatedData)
    {
        return Encrypt(plaintext, additionalAuthenticatedData, 0, 0);
    }
    
    public byte[] Encrypt(
        ArraySegment<byte> plaintext, 
        ArraySegment<byte> additionalAuthenticatedData, 
        uint preBufferSize, 
        uint postBufferSize)
    {                
        // Input validation
        plaintext.Validate();
        additionalAuthenticatedData.Validate();
        
        // used only if plaintext or AAD is empty, since otherwise 'fixed' returns null pointer
        byte dummy; 
        
        fixed (byte* pbPlaintextArray = plaintext.Array)
        {
            fixed (byte* pbAdditionalAuthenticatedDataArray = additionalAuthenticatedData.Array)
            {
                try
                {
                    return EncryptImpl(
                        pbPlaintext: (pbPlaintextArray != null) 
                        	? &pbPlaintextArray[plaintext.Offset] 
                        	: &dummy,
                        cbPlaintext: (uint)plaintext.Count,
                        pbAdditionalAuthenticatedData: (pbAdditionalAuthenticatedDataArray != null) 
                        	? &pbAdditionalAuthenticatedDataArray[additionalAuthenticatedData.Offset] 
                        	: &dummy,
                        cbAdditionalAuthenticatedData: (uint)additionalAuthenticatedData.Count,
                        cbPreBuffer: preBufferSize,
                        cbPostBuffer: postBufferSize);
                }
                catch (Exception ex) when (ex.RequiresHomogenization())
                {
                    // Homogenize to CryptographicException.
                    throw Error.CryptCommon_GenericError(ex);
                }
            }
        }
    }
    
    // 加密算法，在派生类实现
    protected abstract byte[] EncryptImpl(
        byte* pbPlaintext, 
        uint cbPlaintext, 
        byte* pbAdditionalAuthenticatedData, 
        uint cbAdditionalAuthenticatedData, 
        uint cbPreBuffer, 
        uint cbPostBuffer);
    
    // 方法- 解密
    public byte[] Decrypt(
        ArraySegment<byte> ciphertext, 
        ArraySegment<byte> additionalAuthenticatedData)
    {        
        // Input validation
        ciphertext.Validate();
        additionalAuthenticatedData.Validate();
        
        // used only if plaintext or AAD is empty, since otherwise 'fixed' returns null pointer
        byte dummy; 
        fixed (byte* pbCiphertextArray = ciphertext.Array)
        {
            fixed (byte* pbAdditionalAuthenticatedDataArray = additionalAuthenticatedData.Array)
            {
                try
                {
                    return DecryptImpl(
                        pbCiphertext: (pbCiphertextArray != null) 
                        	? &pbCiphertextArray[ciphertext.Offset] 
                        	: &dummy,
                        cbCiphertext: (uint)ciphertext.Count,
                        pbAdditionalAuthenticatedData: (pbAdditionalAuthenticatedDataArray != null) 
                        	? &pbAdditionalAuthenticatedDataArray[additionalAuthenticatedData.Offset] 
                        	: &dummy,
                        cbAdditionalAuthenticatedData: (uint)additionalAuthenticatedData.Count);
                }                 
                catch (Exception ex) when (ex.RequiresHomogenization())          
                {
                    // Homogenize to CryptographicException.
                    throw Error.CryptCommon_GenericError(ex);
                }
            }
        }
    }
    
    // 解密算法，在派生类实现
    protected abstract byte[] DecryptImpl(
        byte* pbCiphertext, 
        uint cbCiphertext, 
        byte* pbAdditionalAuthenticatedData, 
        uint cbAdditionalAuthenticatedData);
        
    public abstract void Dispose();        
}

```

##### 2.5-0.2 bcrypt gen random

```c#
// 接口
internal unsafe interface IBCryptGenRandom
{
    void GenRandom(byte* pbBuffer, uint cbBuffer);
}

// 实现
internal unsafe sealed class BCryptGenRandomImpl : IBCryptGenRandom
{
    public static readonly BCryptGenRandomImpl Instance = new BCryptGenRandomImpl();
    
    private BCryptGenRandomImpl()
    {
    }
    
    public void GenRandom(byte* pbBuffer, uint cbBuffer)
    {
        BCryptUtil.GenRandom(pbBuffer, cbBuffer);
    }
}

```

#### 2.5 cng cbc

##### 2.5.1 descriptor

###### 2.5.1.1 configuration

```c#
[SupportedOSPlatform("windows")]
public sealed class CngCbcAuthenticatedEncryptorConfiguration : 
	AlgorithmConfiguration, 
	IInternalAlgorithmConfiguration
{    
    [ApplyPolicy]
    public string EncryptionAlgorithm { get; set; } = Constants.BCRYPT_AES_ALGORITHM;
        
    [ApplyPolicy]
    public string? EncryptionAlgorithmProvider { get; set; } = null;
           
    [ApplyPolicy]
    public int EncryptionAlgorithmKeySize { get; set; } = 256;
            
    [ApplyPolicy]
    public string HashAlgorithm { get; set; } = Constants.BCRYPT_SHA256_ALGORITHM;
       
    [ApplyPolicy]
    public string? HashAlgorithmProvider { get; set; } = null;
    
    // 方法- create new descriptor => 调用 internal configuration 的 create 方法（secret random）
    public override IAuthenticatedEncryptorDescriptor CreateNewDescriptor()
    {
        var internalConfiguration = (IInternalAlgorithmConfiguration)this;
        return internalConfiguration.CreateDescriptorFromSecret(Secret.Random(KDK_SIZE_IN_BYTES));
    }
            
    IAuthenticatedEncryptorDescriptor IInternalAlgorithmConfiguration.CreateDescriptorFromSecret(ISecret secret)
    {
        return new CngCbcAuthenticatedEncryptorDescriptor(this, secret);
    }
    
    // 方法- validate, 
    // 创建 cng cbc authenticated encryptor factory => encryptor => perform test
    void IInternalAlgorithmConfiguration.Validate()
    {        
        var factory = new CngCbcAuthenticatedEncryptorFactory(NullLoggerFactory.Instance);
        // Run a sample payload through an encrypt -> decrypt operation to make sure data round-trips properly.
        using (var encryptor = factory.CreateAuthenticatedEncryptorInstance(Secret.Random(512 / 8), this))
        {
            encryptor.PerformSelfTest();
        }
    }
}

```

###### 2.5.1.2 descriptor deserializer

```c#
[SupportedOSPlatform("windows")]
public sealed class CngCbcAuthenticatedEncryptorDescriptorDeserializer : IAuthenticatedEncryptorDescriptorDeserializer
{    
    public IAuthenticatedEncryptorDescriptor ImportFromXml(XElement element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }
        
        // <descriptor>
        //   <!-- Windows CNG-CBC -->
        //   <encryption algorithm="..." keyLength="..." [provider="..."] />
        //   <hash algorithm="..." [provider="..."] />
        //   <masterKey>...</masterKey>
        // </descriptor>
        
        // 创建 cng cbc authenticated encryptor configuration（预结果）
        var configuration = new CngCbcAuthenticatedEncryptorConfiguration();
        
        // 从 xelement 解析 "encription" 节，注入 configuration
        var encryptionElement = element.Element("encryption")!;
        configuration.EncryptionAlgorithm = (string)encryptionElement.Attribute("algorithm")!;
        configuration.EncryptionAlgorithmKeySize = (int)encryptionElement.Attribute("keyLength")!;
        configuration.EncryptionAlgorithmProvider = (string?)encryptionElement.Attribute("provider"); // could be null
        
        // 从 xelement 解析 "hash" 节，注入 configuration
        var hashElement = element.Element("hash")!;
        configuration.HashAlgorithm = (string)hashElement.Attribute("algorithm")!;
        configuration.HashAlgorithmProvider = (string?)hashElement.Attribute("provider"); // could be null
        
        // 从 xelement 解析 "masterKey"，转换出 secret
        Secret masterKey = ((string)element.Element("masterKey"))!.ToSecret();
        
        // 创建 cng cbc authenticated encryptor descriptor
        return new CngCbcAuthenticatedEncryptorDescriptor(configuration, masterKey);
    }
}

```

###### 2.5.1.3 descriptor

```c#
[SupportedOSPlatform("windows")]
public sealed class CngCbcAuthenticatedEncryptorDescriptor : IAuthenticatedEncryptorDescriptor
{    
    // master key
    internal ISecret MasterKey { get; }    
    // encryptor configuration
    internal CngCbcAuthenticatedEncryptorConfiguration Configuration { get; }
    
    public CngCbcAuthenticatedEncryptorDescriptor(
        CngCbcAuthenticatedEncryptorConfiguration configuration, 
        ISecret masterKey)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }        
        if (masterKey == null)
        {
            throw new ArgumentNullException(nameof(masterKey));            
        }

        // 注入 configuration
        Configuration = configuration;
        // 注入 master key (secret)
        MasterKey = masterKey;
    }
           
    // 方法- export to xml
    public XmlSerializedDescriptorInfo ExportToXml()
    {
        // <descriptor>
        //   <!-- Windows CNG-CBC -->
        //   <encryption algorithm="..." keyLength="..." [provider="..."] />
        //   <hash algorithm="..." [provider="..."] />
        //   <masterKey>...</masterKey>
        // </descriptor>
        
        // 创建 encryption 节
        var encryptionElement = new XElement(
            "encryption",
            new XAttribute("algorithm", Configuration.EncryptionAlgorithm),
            new XAttribute("keyLength", Configuration.EncryptionAlgorithmKeySize));
        
        if (Configuration.EncryptionAlgorithmProvider != null)
        {
            encryptionElement.SetAttributeValue(
                "provider", 
                Configuration.EncryptionAlgorithmProvider);
        }
        
        // 创建 hash 节
        var hashElement = new XElement(
            "hash",
            new XAttribute("algorithm", Configuration.HashAlgorithm));
        
        if (Configuration.HashAlgorithmProvider != null)
        {
            hashElement.SetAttributeValue("provider", Configuration.HashAlgorithmProvider);
        }
        
        // 创建 xelement，封装 encryption 节、hash 节、master key
        var rootElement = new XElement(
            "descriptor",
            new XComment(" Algorithms provided by Windows CNG, using CBC-mode encryption with HMAC validation"),
            encryptionElement,
            hashElement,
            MasterKey.ToMasterKeyElement());
        
        // 创建 xml serialized descriptor info
        return new XmlSerializedDescriptorInfo(
            rootElement, 
            typeof(CngCbcAuthenticatedEncryptorDescriptorDeserializer));
    }
}

```

##### 2.5.2 encryptor

```c#
internal unsafe sealed class CbcAuthenticatedEncryptor : CngAuthenticatedEncryptorBase
{
    // Even when IVs are chosen randomly, CBC is susceptible to IV collisions within a single key. 
    // For a 64-bit block cipher (like 3DES), we'd expect a collision after 2^32 block encryption operations, 
    // which a high-traffic web server might perform in mere hours.
    // AES and other 128-bit block ciphers are less susceptible to this due to the larger IV space, but unfortunately 
    // some organizations require older 64-bit block ciphers. To address the collision issue, we'll feed 128 bits of entropy 
    // to the KDF when performing subkey generation. This creates >= 192 bits total entropy for each operation, 
    // so we shouldn't expect a collision until >= 2^96 operations. Even 2^80 operations still maintains a <= 2^-32
    // probability of collision, and this is acceptable for the expected KDK lifetime.
    private const uint KEY_MODIFIER_SIZE_IN_BYTES = 128 / 8;
    
    private readonly byte[] _contextHeader;
    private readonly IBCryptGenRandom _genRandom;
    private readonly BCryptAlgorithmHandle _hmacAlgorithmHandle;
    private readonly uint _hmacAlgorithmDigestLengthInBytes;
    private readonly uint _hmacAlgorithmSubkeyLengthInBytes;
    private readonly ISP800_108_CTR_HMACSHA512Provider _sp800_108_ctr_hmac_provider;
    private readonly BCryptAlgorithmHandle _symmetricAlgorithmHandle;
    private readonly uint _symmetricAlgorithmBlockSizeInBytes;
    private readonly uint _symmetricAlgorithmSubkeyLengthInBytes;
    
    public CbcAuthenticatedEncryptor(        
        Secret keyDerivationKey, 	// master key
        BCryptAlgorithmHandle symmetricAlgorithmHandle, 
        uint symmetricAlgorithmKeySizeInBytes, 
        BCryptAlgorithmHandle hmacAlgorithmHandle, 
        IBCryptGenRandom? genRandom = null)
    {
        // 注入 gen random
        _genRandom = genRandom ?? BCryptGenRandomImpl.Instance;
        // 由 key derivation (master key) 创建 hmac provider
        _sp800_108_ctr_hmac_provider = SP800_108_CTR_HMACSHA512Util.CreateProvider(keyDerivationKey);
        
        // symmetric algorithm
        _symmetricAlgorithmHandle = symmetricAlgorithmHandle;
        _symmetricAlgorithmBlockSizeInBytes = symmetricAlgorithmHandle.GetCipherBlockLength();
        _symmetricAlgorithmSubkeyLengthInBytes = symmetricAlgorithmKeySizeInBytes;
        
        // hmac algorithm
        _hmacAlgorithmHandle = hmacAlgorithmHandle;
        _hmacAlgorithmDigestLengthInBytes = hmacAlgorithmHandle.GetHashDigestLength();
        // for simplicity we'll generate HMAC subkeys with a length equal to the digest length
        _hmacAlgorithmSubkeyLengthInBytes = _hmacAlgorithmDigestLengthInBytes; 

        // Argument checking on the algorithms and lengths passed in to us
        AlgorithmAssert.IsAllowableSymmetricAlgorithmBlockSize(checked(_symmetricAlgorithmBlockSizeInBytes * 8));
        AlgorithmAssert.IsAllowableSymmetricAlgorithmKeySize(checked(_symmetricAlgorithmSubkeyLengthInBytes * 8));
        AlgorithmAssert.IsAllowableValidationAlgorithmDigestSize(checked(_hmacAlgorithmDigestLengthInBytes * 8));
        
        _contextHeader = CreateContextHeader();
    }
    
    private byte[] CreateContextHeader()
    {
        var retVal = new byte[checked(
            1 /* KDF alg */
            + 1 /* chaining mode */
            + sizeof(uint) /* sym alg key size */
            + sizeof(uint) /* sym alg block size */
            + sizeof(uint) /* hmac alg key size */
            + sizeof(uint) /* hmac alg digest size */
            + _symmetricAlgorithmBlockSizeInBytes /* ciphertext of encrypted empty string */
            + _hmacAlgorithmDigestLengthInBytes /* digest of HMACed empty string */)];
        
        fixed (byte* pbRetVal = retVal)
        {
            byte* ptr = pbRetVal;
            
            // First is the two-byte header
            *(ptr++) = 0; // 0x00 = SP800-108 CTR KDF w/ HMACSHA512 PRF
            *(ptr++) = 0; // 0x00 = CBC encryption + HMAC authentication
            
            // Next is information about the symmetric algorithm (key size followed by block size)
            BitHelpers.WriteTo(ref ptr, _symmetricAlgorithmSubkeyLengthInBytes);
            BitHelpers.WriteTo(ref ptr, _symmetricAlgorithmBlockSizeInBytes);
            
            // Next is information about the HMAC algorithm (key size followed by digest size)
            BitHelpers.WriteTo(ref ptr, _hmacAlgorithmSubkeyLengthInBytes);
            BitHelpers.WriteTo(ref ptr, _hmacAlgorithmDigestLengthInBytes);
            
            // See the design document for an explanation of the following code.
            var tempKeys = new byte[_symmetricAlgorithmSubkeyLengthInBytes + _hmacAlgorithmSubkeyLengthInBytes];
            fixed (byte* pbTempKeys = tempKeys)
            {
                byte dummy;
                
                // Derive temporary keys for encryption + HMAC.
                using (var provider = SP800_108_CTR_HMACSHA512Util.CreateEmptyProvider())
                {
                    provider.DeriveKey(
                        pbLabel: &dummy,
                        cbLabel: 0,
                        pbContext: &dummy,
                        cbContext: 0,
                        pbDerivedKey: pbTempKeys,
                        cbDerivedKey: (uint)tempKeys.Length);
                }
                
                // At this point, tempKeys := { K_E || K_H }.
                byte* pbSymmetricEncryptionSubkey = pbTempKeys;
                byte* pbHmacSubkey = &pbTempKeys[_symmetricAlgorithmSubkeyLengthInBytes];
                
                // Encrypt a zero-length input string with an all-zero IV and copy the ciphertext 
                // to the return buffer.
                using (var symmetricKeyHandle = _symmetricAlgorithmHandle.GenerateSymmetricKey(
                    		pbSymmetricEncryptionSubkey, 
                    		_symmetricAlgorithmSubkeyLengthInBytes))
                {
                    /* will be zero-initialized */
                    fixed (byte* pbIV = new byte[_symmetricAlgorithmBlockSizeInBytes] )
                    {
                        DoCbcEncrypt(
                            symmetricKeyHandle: symmetricKeyHandle,
                            pbIV: pbIV,
                            pbInput: &dummy,
                            cbInput: 0,
                            pbOutput: ptr,
                            cbOutput: _symmetricAlgorithmBlockSizeInBytes);
                    }
                }
                ptr += _symmetricAlgorithmBlockSizeInBytes;

                // MAC a zero-length input string and copy the digest to the return buffer.
                using (var hashHandle = 
                       	   _hmacAlgorithmHandle.CreateHmac(
                               pbHmacSubkey, 
                               _hmacAlgorithmSubkeyLengthInBytes))
                {
                    hashHandle.HashData(
                        pbInput: &dummy,
                        cbInput: 0,
                        pbHashDigest: ptr,
                        cbHashDigest: _hmacAlgorithmDigestLengthInBytes);
                }
                
                ptr += _hmacAlgorithmDigestLengthInBytes;
                CryptoUtil.Assert(ptr - pbRetVal == retVal.Length, "ptr - pbRetVal == retVal.Length");
            }
        }
        
        // retVal := 
        //   { version || chainingMode || symAlgKeySize || symAlgBlockSize || hmacAlgKeySize || hmacAlgDigestSize || E("") || MAC("") }.
        return retVal;
    }
            
    public override void Dispose()
    {
        _sp800_108_ctr_hmac_provider.Dispose();
        
        // We don't want to dispose of the underlying algorithm instances because they might be reused.
    }                                       
}

```

###### 2.5.2.1 基类方法 - encrypt impl

```c#
internal unsafe sealed class CbcAuthenticatedEncryptor : CngAuthenticatedEncryptorBase
{
    protected override byte[] EncryptImpl(
        byte* pbPlaintext, 
        uint cbPlaintext, 
        byte* pbAdditionalAuthenticatedData, 
        uint cbAdditionalAuthenticatedData, 
        uint cbPreBuffer, 
        uint cbPostBuffer)
    {
        // 分配 byte[] of "temp sub keys"
        // This buffer will be used to hold the symmetric encryption and HMAC subkeys used in the generation of this payload.
        var cbTempSubkeys = checked(_symmetricAlgorithmSubkeyLengthInBytes + _hmacAlgorithmSubkeyLengthInBytes);
        byte* pbTempSubkeys = stackalloc byte[checked((int)cbTempSubkeys)];
        
        try
        {            
            // Randomly generate the key modifier and IV.
            var cbKeyModifierAndIV = checked(
                KEY_MODIFIER_SIZE_IN_BYTES + 
                _symmetricAlgorithmBlockSizeInBytes);
            byte* pbKeyModifierAndIV = stackalloc byte[checked((int)cbKeyModifierAndIV)];
            _genRandom.GenRandom(pbKeyModifierAndIV, cbKeyModifierAndIV);
            
            // Calculate offsets
            byte* pbKeyModifier = pbKeyModifierAndIV;
            byte* pbIV = &pbKeyModifierAndIV[KEY_MODIFIER_SIZE_IN_BYTES];
            
            // Use the KDF to generate a new symmetric encryption and HMAC subkey
            _sp800_108_ctr_hmac_provider.DeriveKeyWithContextHeader(
                pbLabel: pbAdditionalAuthenticatedData,
                cbLabel: cbAdditionalAuthenticatedData,
                contextHeader: _contextHeader,
                pbContext: pbKeyModifier,
                cbContext: KEY_MODIFIER_SIZE_IN_BYTES,
                pbDerivedKey: pbTempSubkeys,
                cbDerivedKey: cbTempSubkeys);
            
            // Calculate offsets
            byte* pbSymmetricEncryptionSubkey = pbTempSubkeys;
            byte* pbHmacSubkey = &pbTempSubkeys[_symmetricAlgorithmSubkeyLengthInBytes];
            
            using (var symmetricKeyHandle = _symmetricAlgorithmHandle.GenerateSymmetricKey(
                	   pbSymmetricEncryptionSubkey, 
                	   _symmetricAlgorithmSubkeyLengthInBytes))
            {
                // We can't assume PKCS#7 padding (maybe the underlying provider is really using CTS),
                // so we need to query the padded output size before we can allocate the return value array.
                var cbOutputCiphertext = GetCbcEncryptedOutputSizeWithPadding(
                    symmetricKeyHandle, 
                    pbPlaintext, 
                    cbPlaintext);

                // Allocate return value array and start copying some data
                var retVal = new byte[checked(
                    cbPreBuffer + 
                    KEY_MODIFIER_SIZE_IN_BYTES + 
                    _symmetricAlgorithmBlockSizeInBytes + 
                    cbOutputCiphertext + 
                    _hmacAlgorithmDigestLengthInBytes + cbPostBuffer)];
                
                fixed (byte* pbRetVal = retVal)
                {
                    // Calculate offsets
                    byte* pbOutputKeyModifier = &pbRetVal[cbPreBuffer];
                    byte* pbOutputIV = &pbOutputKeyModifier[KEY_MODIFIER_SIZE_IN_BYTES];
                    byte* pbOutputCiphertext = &pbOutputIV[_symmetricAlgorithmBlockSizeInBytes];
                    byte* pbOutputHmac = &pbOutputCiphertext[cbOutputCiphertext];
                    
                    UnsafeBufferUtil.BlockCopy(
                        from: pbKeyModifierAndIV, 
                        to: pbOutputKeyModifier, 
                        byteCount: cbKeyModifierAndIV);
                    
                    // retVal will eventually contain 
                    // 	   { preBuffer | keyModifier | iv | encryptedData | HMAC(iv | encryptedData) | postBuffer }
                    // At this point, retVal := { preBuffer | keyModifier | iv | _____ | _____ | postBuffer }

                    DoCbcEncrypt(
                        symmetricKeyHandle: symmetricKeyHandle,
                        pbIV: pbIV,
                        pbInput: pbPlaintext,
                        cbInput: cbPlaintext,
                        pbOutput: pbOutputCiphertext,
                        cbOutput: cbOutputCiphertext);
                    
                    // At this point, retVal := 
                    //     { preBuffer | keyModifier | iv | encryptedData | _____ | postBuffer }

                    // Compute the HMAC over the IV and the ciphertext (prevents IV tampering).
                    // The HMAC is already implicitly computed over the key modifier since the key
                    // modifier is used as input to the KDF.
                    using (var hashHandle = _hmacAlgorithmHandle.CreateHmac(
                        	   pbHmacSubkey, 
                        	   _hmacAlgorithmSubkeyLengthInBytes))
                    {
                        hashHandle.HashData(
                            pbInput: pbOutputIV,
                            cbInput: checked(_symmetricAlgorithmBlockSizeInBytes + cbOutputCiphertext),
                            pbHashDigest: pbOutputHmac,
                            cbHashDigest: _hmacAlgorithmDigestLengthInBytes);
                    }
                    
                    // At this point, retVal := 
                    //     { preBuffer | keyModifier | iv | encryptedData | HMAC(iv | encryptedData) | postBuffer }
                    // And we're done!
                    return retVal;
                }
            }
        }
        finally
        {
            // Buffer contains sensitive material; delete it.
            UnsafeBufferUtil.SecureZeroMemory(pbTempSubkeys, cbTempSubkeys);
        }
    }
    
    private uint GetCbcEncryptedOutputSizeWithPadding(
        BCryptKeyHandle symmetricKeyHandle, 
        byte* pbInput, 
        uint cbInput)
    {
        // ok for this memory to remain uninitialized since nobody depends on it
        byte* pbIV = stackalloc byte[checked((int)_symmetricAlgorithmBlockSizeInBytes)];
        
        // Calling BCryptEncrypt with a null output pointer will cause it to return the total number
        // of bytes required for the output buffer.
        uint dwResult;
        var ntstatus = UnsafeNativeMethods.BCryptEncrypt(
            hKey: symmetricKeyHandle,
            pbInput: pbInput,
            cbInput: cbInput,
            pPaddingInfo: null,
            pbIV: pbIV,
            cbIV: _symmetricAlgorithmBlockSizeInBytes,
            pbOutput: null,
            cbOutput: 0,
            pcbResult: out dwResult,
            dwFlags: BCryptEncryptFlags.BCRYPT_BLOCK_PADDING);
        UnsafeNativeMethods.ThrowExceptionForBCryptStatus(ntstatus);
        
        return dwResult;
    }
    
    // 'pbIV' must be a pointer to a buffer equal in length to the symmetric algorithm block size.
    private void DoCbcEncrypt(
        BCryptKeyHandle symmetricKeyHandle, 
        byte* pbIV, 
        byte* pbInput, 
        uint cbInput, 
        byte* pbOutput, 
        uint cbOutput)
    {
        // BCryptEncrypt mutates the provided IV; we need to clone it to prevent mutation of the original value
        byte* pbClonedIV = stackalloc byte[checked((int)_symmetricAlgorithmBlockSizeInBytes)];
        UnsafeBufferUtil.BlockCopy(
            from: pbIV, 
            to: pbClonedIV, 
            byteCount: _symmetricAlgorithmBlockSizeInBytes);
        
        uint dwEncryptedBytes;
        var ntstatus = UnsafeNativeMethods.BCryptEncrypt(                
            hKey: symmetricKeyHandle,
            pbInput: pbInput,
            cbInput: cbInput,
            pPaddingInfo: null,
            pbIV: pbClonedIV,
            cbIV: _symmetricAlgorithmBlockSizeInBytes,
            pbOutput: pbOutput,
            cbOutput: cbOutput,
            pcbResult: out dwEncryptedBytes,
            dwFlags: BCryptEncryptFlags.BCRYPT_BLOCK_PADDING);
        UnsafeNativeMethods.ThrowExceptionForBCryptStatus(ntstatus);
        
        // Need to make sure we didn't underrun the buffer - means caller passed a bad value
        CryptoUtil.Assert(dwEncryptedBytes == cbOutput, "dwEncryptedBytes == cbOutput");
    }
    
}

```

###### 2.5.2.2 基类方法 - decrypt impl

```c#
internal unsafe sealed class CbcAuthenticatedEncryptor : CngAuthenticatedEncryptorBase
{
    protected override byte[] DecryptImpl(
        byte* pbCiphertext, 
        uint cbCiphertext, 
        byte* pbAdditionalAuthenticatedData, 
        uint cbAdditionalAuthenticatedData)
    {
        // Argument checking - input must at the absolute minimum contain a key modifier, IV, and MAC
        if (cbCiphertext < checked(
            	KEY_MODIFIER_SIZE_IN_BYTES + 
            	_symmetricAlgorithmBlockSizeInBytes + 
            	_hmacAlgorithmDigestLengthInBytes))
        {
            throw Error.CryptCommon_PayloadInvalid();
        }
        
        // Assumption: pbCipherText := 
        //   { keyModifier | IV | encryptedData | MAC(IV | encryptedPayload) }
        
        var cbEncryptedData = checked(
            cbCiphertext - 
            (KEY_MODIFIER_SIZE_IN_BYTES + 
             _symmetricAlgorithmBlockSizeInBytes + 
             _hmacAlgorithmDigestLengthInBytes));
        
        // Calculate offsets
        byte* pbKeyModifier = pbCiphertext;
        byte* pbIV = &pbKeyModifier[KEY_MODIFIER_SIZE_IN_BYTES];
        byte* pbEncryptedData = &pbIV[_symmetricAlgorithmBlockSizeInBytes];
        byte* pbActualHmac = &pbEncryptedData[cbEncryptedData];
        
        // Use the KDF to recreate the symmetric encryption and HMAC subkeys
        // We'll need a temporary buffer to hold them
        var cbTempSubkeys = checked(
            _symmetricAlgorithmSubkeyLengthInBytes + 
            _hmacAlgorithmSubkeyLengthInBytes);
        
        byte* pbTempSubkeys = stackalloc byte[checked((int)cbTempSubkeys)];
        try
        {
            _sp800_108_ctr_hmac_provider.DeriveKeyWithContextHeader(
                pbLabel: pbAdditionalAuthenticatedData,
                cbLabel: cbAdditionalAuthenticatedData,
                contextHeader: _contextHeader,
                pbContext: pbKeyModifier,
                cbContext: KEY_MODIFIER_SIZE_IN_BYTES,
                pbDerivedKey: pbTempSubkeys,
                cbDerivedKey: cbTempSubkeys);
            
            // Calculate offsets
            byte* pbSymmetricEncryptionSubkey = pbTempSubkeys;
            byte* pbHmacSubkey = &pbTempSubkeys[_symmetricAlgorithmSubkeyLengthInBytes];
            
            // First, perform an explicit integrity check over (iv | encryptedPayload) to ensure the data hasn't been tampered with.
            // The integrity check is also implicitly performed over keyModifier since that value was provided to the KDF earlier.
            using (var hashHandle = _hmacAlgorithmHandle.CreateHmac(
                	   pbHmacSubkey, 
                	   _hmacAlgorithmSubkeyLengthInBytes))
            {
                if (!ValidateHash(
                    	hashHandle, 
                    	pbIV, 
                    	_symmetricAlgorithmBlockSizeInBytes + cbEncryptedData, 
                    	pbActualHmac))
                {
                    throw Error.CryptCommon_PayloadInvalid();
                }
            }
            
            // If the integrity check succeeded, decrypt the payload.
            using (var decryptionSubkeyHandle = _symmetricAlgorithmHandle.GenerateSymmetricKey(
                	   pbSymmetricEncryptionSubkey, 
                	   _symmetricAlgorithmSubkeyLengthInBytes))
            {
                return DoCbcDecrypt(
                    decryptionSubkeyHandle, 
                    pbIV, 
                    pbEncryptedData, 
                    cbEncryptedData);
            }
        }
        finally
        {
            // Buffer contains sensitive key material; delete.
            UnsafeBufferUtil.SecureZeroMemory(pbTempSubkeys, cbTempSubkeys);
        }
    }
    
    // 'pbIV' must be a pointer to a buffer equal in length to the symmetric algorithm block size.
    private byte[] DoCbcDecrypt(
        BCryptKeyHandle symmetricKeyHandle, byte* pbIV, byte* pbInput, uint cbInput)
    {
        // BCryptDecrypt mutates the provided IV; we need to clone it to prevent mutation of the original value
        byte* pbClonedIV = stackalloc byte[checked((int)_symmetricAlgorithmBlockSizeInBytes)];
        UnsafeBufferUtil.BlockCopy(
            from: pbIV, 
            to: pbClonedIV, 
            byteCount: _symmetricAlgorithmBlockSizeInBytes);
        
        // First, figure out how large an output buffer we require.
        // Ideally we'd be able to transform the last block ourselves and strip
        // off the padding before creating the return value array, but we don't
            // know the actual padding scheme being used under the covers (we can't
            // assume PKCS#7). So unfortunately we're stuck with the temporary buffer.
            // (Querying the output size won't mutate the IV.)
        uint dwEstimatedDecryptedByteCount;
        var ntstatus = UnsafeNativeMethods.BCryptDecrypt(
            hKey: symmetricKeyHandle,
            pbInput: pbInput,
            cbInput: cbInput,
            pPaddingInfo: null,
            pbIV: pbClonedIV,
            cbIV: _symmetricAlgorithmBlockSizeInBytes,
            pbOutput: null,
            cbOutput: 0,
            pcbResult: out dwEstimatedDecryptedByteCount,
            dwFlags: BCryptEncryptFlags.BCRYPT_BLOCK_PADDING);
        UnsafeNativeMethods.ThrowExceptionForBCryptStatus(ntstatus);
        
        var decryptedPayload = new byte[dwEstimatedDecryptedByteCount];
        uint dwActualDecryptedByteCount;
        fixed (byte* pbDecryptedPayload = decryptedPayload)
        {
            byte dummy;
            
            // Perform the actual decryption.
            ntstatus = UnsafeNativeMethods.BCryptDecrypt(
                hKey: symmetricKeyHandle,
                pbInput: pbInput,
                cbInput: cbInput,
                pPaddingInfo: null,
                pbIV: pbClonedIV,
                cbIV: _symmetricAlgorithmBlockSizeInBytes,
                pbOutput: (pbDecryptedPayload != null) ? pbDecryptedPayload : &dummy, // CLR won't pin zero-length arrays
                cbOutput: dwEstimatedDecryptedByteCount,
                pcbResult: out dwActualDecryptedByteCount,
                dwFlags: BCryptEncryptFlags.BCRYPT_BLOCK_PADDING);
            UnsafeNativeMethods.ThrowExceptionForBCryptStatus(ntstatus);
        }
        
        // Decryption finished!
        CryptoUtil.Assert(
            dwActualDecryptedByteCount <= dwEstimatedDecryptedByteCount, 
            "dwActualDecryptedByteCount <= dwEstimatedDecryptedByteCount");
        if (dwActualDecryptedByteCount == dwEstimatedDecryptedByteCount)
        {
            // payload takes up the entire buffer
            return decryptedPayload;
        }
        else
        {
            // payload takes up only a partial buffer
            var resizedDecryptedPayload = new byte[dwActualDecryptedByteCount];
            Buffer.BlockCopy(
                decryptedPayload, 
                0, 
                resizedDecryptedPayload, 
                0, 
                resizedDecryptedPayload.Length);
            return resizedDecryptedPayload;
        }
    }
    
    // 'pbExpectedDigest' must point to a '_hmacAlgorithmDigestLengthInBytes'-length buffer
    private bool ValidateHash(
        BCryptHashHandle hashHandle, 
        byte* pbInput, 
        uint cbInput, 
        byte* pbExpectedDigest)
    {
        byte* pbActualDigest = stackalloc byte[checked((int)_hmacAlgorithmDigestLengthInBytes)];
        
        hashHandle.HashData(
            pbInput, 
            cbInput, 
            pbActualDigest, 
            _hmacAlgorithmDigestLengthInBytes);
        
        return CryptoUtil.TimeConstantBuffersAreEqual(
            pbExpectedDigest, 
            pbActualDigest, 
            _hmacAlgorithmDigestLengthInBytes);
    }
}

```



##### 2.5.3 encryptor factory

```c#
public sealed class CngCbcAuthenticatedEncryptorFactory : IAuthenticatedEncryptorFactory
{
    private readonly ILogger _logger;
        
    public CngCbcAuthenticatedEncryptorFactory(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CngCbcAuthenticatedEncryptorFactory>();
    }
    
    // 方法- 由 key 创建 encryptor
    public IAuthenticatedEncryptor? CreateEncryptorInstance(IKey key)
    {
        if (key.Descriptor is not CngCbcAuthenticatedEncryptorDescriptor descriptor)
        {
            return null;
        }
        
        Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        return CreateAuthenticatedEncryptorInstance(
            descriptor.MasterKey, 
            descriptor.Configuration);
    }
    
    [SupportedOSPlatform("windows")]
    [return: NotNullIfNotNull("configuration")]
    internal CbcAuthenticatedEncryptor? CreateAuthenticatedEncryptorInstance(
        ISecret secret,
        CngCbcAuthenticatedEncryptorConfiguration? configuration)
    {
        if (configuration == null)
        {
            return null;
        }
        
        return new CbcAuthenticatedEncryptor(
            // master key
            keyDerivationKey: new Secret(secret),
            // 1-
            symmetricAlgorithmHandle: GetSymmetricBlockCipherAlgorithmHandle(configuration),           
            symmetricAlgorithmKeySizeInBytes: (uint)(configuration.EncryptionAlgorithmKeySize / 8),
            // 2-
            hmacAlgorithmHandle: GetHmacAlgorithmHandle(configuration));
    }
    
    // 2-
    [SupportedOSPlatform("windows")]
    private BCryptAlgorithmHandle GetHmacAlgorithmHandle(CngCbcAuthenticatedEncryptorConfiguration configuration)
    {
        // basic argument checking
        if (String.IsNullOrEmpty(configuration.HashAlgorithm))
        {
            throw Error.Common_PropertyCannotBeNullOrEmpty(nameof(configuration.HashAlgorithm));
        }
        
        _logger.OpeningCNGAlgorithmFromProviderWithHMAC(configuration.HashAlgorithm, configuration.HashAlgorithmProvider);
        BCryptAlgorithmHandle? algorithmHandle = null;
        
        // Special-case cached providers
        if (configuration.HashAlgorithmProvider == null)
        {
            if (configuration.HashAlgorithm == Constants.BCRYPT_SHA1_ALGORITHM) 
            { 
                algorithmHandle = CachedAlgorithmHandles.HMAC_SHA1; 
            }
            else if (configuration.HashAlgorithm == Constants.BCRYPT_SHA256_ALGORITHM) 
            {
                algorithmHandle = CachedAlgorithmHandles.HMAC_SHA256; 
            }
            else if (configuration.HashAlgorithm == Constants.BCRYPT_SHA512_ALGORITHM) 
            {
                algorithmHandle = CachedAlgorithmHandles.HMAC_SHA512; 
            }
        }
        
        // Look up the provider dynamically if we couldn't fetch a cached instance
        if (algorithmHandle == null)
        {
            algorithmHandle = BCryptAlgorithmHandle.OpenAlgorithmHandle(
                configuration.HashAlgorithm, 
                configuration.HashAlgorithmProvider, 
                hmac: true);
        }
        
        // Make sure we're using a hash algorithm. We require a minimum 128-bit digest.
        uint digestSize = algorithmHandle.GetHashDigestLength();
        AlgorithmAssert.IsAllowableValidationAlgorithmDigestSize(checked(digestSize * 8));
        
        // all good!
        return algorithmHandle;
    }
    
    // 1- 
    [SupportedOSPlatform("windows")]
    private BCryptAlgorithmHandle GetSymmetricBlockCipherAlgorithmHandle(CngCbcAuthenticatedEncryptorConfiguration configuration)
    {
        // basic argument checking
        if (String.IsNullOrEmpty(configuration.EncryptionAlgorithm))
        {
            throw Error.Common_PropertyCannotBeNullOrEmpty(nameof(EncryptionAlgorithm));
        }
        if (configuration.EncryptionAlgorithmKeySize < 0)
        {
            throw Error.Common_PropertyMustBeNonNegative(nameof(configuration.EncryptionAlgorithmKeySize));
        }
        
        _logger.OpeningCNGAlgorithmFromProviderWithChainingModeCBC(
            configuration.EncryptionAlgorithm, 
            configuration.EncryptionAlgorithmProvider);
        
        BCryptAlgorithmHandle? algorithmHandle = null;
        
        // Special-case cached providers
        if (configuration.EncryptionAlgorithmProvider == null)
        {
            if (configuration.EncryptionAlgorithm == Constants.BCRYPT_AES_ALGORITHM) 
            {
                algorithmHandle = CachedAlgorithmHandles.AES_CBC; 
            }
        }
        
        // Look up the provider dynamically if we couldn't fetch a cached instance
        if (algorithmHandle == null)
        {
            algorithmHandle = BCryptAlgorithmHandle.OpenAlgorithmHandle(
                configuration.EncryptionAlgorithm, 
                configuration.EncryptionAlgorithmProvider);
            
            algorithmHandle.SetChainingMode(Constants.BCRYPT_CHAIN_MODE_CBC);
        }
        
        // make sure we're using a block cipher with an appropriate key size & block size
        AlgorithmAssert.IsAllowableSymmetricAlgorithmBlockSize(checked(algorithmHandle.GetCipherBlockLength() * 8));
        AlgorithmAssert.IsAllowableSymmetricAlgorithmKeySize(checked((uint)configuration.EncryptionAlgorithmKeySize));
        
        // make sure the provided key length is valid
        algorithmHandle.GetSupportedKeyLengths().EnsureValidKeyLength((uint)configuration.EncryptionAlgorithmKeySize);
        
        // all good!
        return algorithmHandle;
    }
}

```

#### 2.6 cng gcm

##### 2.6.1 descriptor

###### 2.6.1.1 configuration

```c#
[SupportedOSPlatform("windows")]
public sealed class CngGcmAuthenticatedEncryptorConfiguration : 
	AlgorithmConfiguration, 
	IInternalAlgorithmConfiguration
{    
    [ApplyPolicy]
    public string EncryptionAlgorithm { get; set; } = Constants.BCRYPT_AES_ALGORITHM;
            
    [ApplyPolicy]
    public string? EncryptionAlgorithmProvider { get; set; } = null;
           
    [ApplyPolicy]
    public int EncryptionAlgorithmKeySize { get; set; } = 256;
    
    // 方法- create new descriptor，调用 internal configuration 的 create 方法（secret random）
    public override IAuthenticatedEncryptorDescriptor CreateNewDescriptor()
    {
        var internalConfiguration = (IInternalAlgorithmConfiguration)this;
        return internalConfiguration.CreateDescriptorFromSecret(Secret.Random(KDK_SIZE_IN_BYTES));
    }
    
    IAuthenticatedEncryptorDescriptor IInternalAlgorithmConfiguration.CreateDescriptorFromSecret(ISecret secret)
    {
        return new CngGcmAuthenticatedEncryptorDescriptor(this, secret);
    }
    
    // 方法- validate，
    // 创建 cng gcm encryptor factory => encryptor => perform test
    void IInternalAlgorithmConfiguration.Validate()
    {        
        var factory = new CngGcmAuthenticatedEncryptorFactory(NullLoggerFactory.Instance);
        // Run a sample payload through an encrypt -> decrypt operation to make sure data round-trips properly.
        using (var encryptor = factory.CreateAuthenticatedEncryptorInstance(Secret.Random(512 / 8), this))
        {
            encryptor.PerformSelfTest();
        }
    }
}

```

###### 2.6.1.2 descriptor deserializer

```c#
[SupportedOSPlatform("windows")]
public sealed class CngGcmAuthenticatedEncryptorDescriptorDeserializer : IAuthenticatedEncryptorDescriptorDeserializer
{        
    public IAuthenticatedEncryptorDescriptor ImportFromXml(XElement element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }
        
        // <descriptor>
        //   <!-- Windows CNG-GCM -->
        //   <encryption algorithm="..." keyLength="..." [provider="..."] />
        //   <masterKey>...</masterKey>
        // </descriptor>
        
        // 创建 cng gcm configuration（预结果）
        var configuration = new CngGcmAuthenticatedEncryptorConfiguration();
        
        // 从 xelement 解析 "encryption"，注入 configuration（预结果）
        var encryptionElement = element.Element("encryption")!;
        configuration.EncryptionAlgorithm = (string)encryptionElement.Attribute("algorithm")!;
        configuration.EncryptionAlgorithmKeySize = (int)encryptionElement.Attribute("keyLength")!;
        configuration.EncryptionAlgorithmProvider = (string?)encryptionElement.Attribute("provider"); // could be null
        
        // 从 xelement 解析 "masterKey"，转换为 secret
        Secret masterKey = ((string)element.Element("masterKey")!).ToSecret();
        
        // 创建 cng gcm encryptor descriptor
        return new CngGcmAuthenticatedEncryptorDescriptor(configuration, masterKey);
    }
}

```

###### 2.6.1.3 descriptor

```c#
[SupportedOSPlatform("windows")]
public sealed class CngGcmAuthenticatedEncryptorDescriptor : IAuthenticatedEncryptorDescriptor
{    
    internal ISecret MasterKey { get; }    
    internal CngGcmAuthenticatedEncryptorConfiguration Configuration { get; }
    
    public CngGcmAuthenticatedEncryptorDescriptor(
        CngGcmAuthenticatedEncryptorConfiguration configuration, 
        ISecret masterKey)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }        
        if (masterKey == null)
        {
            throw new ArgumentNullException(nameof(masterKey));
        }
        
        Configuration = configuration;
        MasterKey = masterKey;
    }
            
    // 方法- export to xml
    public XmlSerializedDescriptorInfo ExportToXml()
    {
        // <descriptor>
        //   <!-- Windows CNG-GCM -->
        //   <encryption algorithm="..." keyLength="..." [provider="..."] />
        //   <masterKey>...</masterKey>
        // </descriptor>
        
        // 创建 encryption 节
        var encryptionElement = new XElement(
            "encryption",
            new XAttribute("algorithm", Configuration.EncryptionAlgorithm),
            new XAttribute("keyLength", Configuration.EncryptionAlgorithmKeySize));
        
        if (Configuration.EncryptionAlgorithmProvider != null)
        {
            encryptionElement.SetAttributeValue("provider", Configuration.EncryptionAlgorithmProvider);
        }
        
        // 创建 xelement，封装 encryption 节、master key
        var rootElement = new XElement(
            "descriptor",
            new XComment(" Algorithms provided by Windows CNG, using Galois/Counter Mode encryption and validation "),
            encryptionElement,
            MasterKey.ToMasterKeyElement());
        
        // 创建 xml serialized descriptor info
        return new XmlSerializedDescriptorInfo(
            rootElement, 
            typeof(CngGcmAuthenticatedEncryptorDescriptorDeserializer));
    }
}

```

##### 2.6.2 encryptor

```c#
internal unsafe sealed class CngGcmAuthenticatedEncryptor : CngAuthenticatedEncryptorBase
{
    // Having a key modifier ensures with overwhelming probability that no two encryption operations will ever derive 
    // the same (encryption subkey, MAC subkey) pair. 
    // This limits an attacker's ability to mount a key-dependent chosen ciphertext attack. See also the class-level comment
    // for how this is used to overcome GCM's IV limitations.
    private const uint KEY_MODIFIER_SIZE_IN_BYTES = 128 / 8;    
    private const uint NONCE_SIZE_IN_BYTES = 96 / 8; 	// GCM has a fixed 96-bit IV
    private const uint TAG_SIZE_IN_BYTES = 128 / 8; 	// we're hardcoding a 128-bit authentication tag size
    
    private readonly byte[] _contextHeader;
    private readonly IBCryptGenRandom _genRandom;
    private readonly ISP800_108_CTR_HMACSHA512Provider _sp800_108_ctr_hmac_provider;
    private readonly BCryptAlgorithmHandle _symmetricAlgorithmHandle;
    private readonly uint _symmetricAlgorithmSubkeyLengthInBytes;
    
    public CngGcmAuthenticatedEncryptor(
        Secret keyDerivationKey, 
        BCryptAlgorithmHandle symmetricAlgorithmHandle, 
        uint symmetricAlgorithmKeySizeInBytes, 
        IBCryptGenRandom? genRandom = null)
    {
        // Is the key size appropriate?
        AlgorithmAssert.IsAllowableSymmetricAlgorithmKeySize(checked(symmetricAlgorithmKeySizeInBytes * 8));
        CryptoUtil.Assert(
            symmetricAlgorithmHandle.GetCipherBlockLength() == 128 / 8, 
            "GCM requires a block cipher algorithm with a 128-bit block size.");
        
        // 注入 gen random
        _genRandom = genRandom ?? BCryptGenRandomImpl.Instance;
        // 由 key derivateion (master key) 创建 hmac provider
        _sp800_108_ctr_hmac_provider = SP800_108_CTR_HMACSHA512Util.CreateProvider(keyDerivationKey);
        
        // symmetric algorithm
        _symmetricAlgorithmHandle = symmetricAlgorithmHandle;
        _symmetricAlgorithmSubkeyLengthInBytes = symmetricAlgorithmKeySizeInBytes;
        
        _contextHeader = CreateContextHeader();
    }
    
    private byte[] CreateContextHeader()
    {
        var retVal = new byte[checked(
            1 /* KDF alg */
            + 1 /* chaining mode */
            + sizeof(uint) /* sym alg key size */
            + sizeof(uint) /* GCM nonce size */
            + sizeof(uint) /* sym alg block size */
            + sizeof(uint) /* GCM tag size */
            + TAG_SIZE_IN_BYTES /* tag of GCM-encrypted empty string */)];
        
        fixed (byte* pbRetVal = retVal)
        {
            byte* ptr = pbRetVal;
            
            // First is the two-byte header
            *(ptr++) = 0; // 0x00 = SP800-108 CTR KDF w/ HMACSHA512 PRF
            *(ptr++) = 1; // 0x01 = GCM encryption + authentication
            
            // Next is information about the symmetric algorithm (key size, nonce size, block size, tag size)
            BitHelpers.WriteTo(ref ptr, _symmetricAlgorithmSubkeyLengthInBytes);
            BitHelpers.WriteTo(ref ptr, NONCE_SIZE_IN_BYTES);
            BitHelpers.WriteTo(ref ptr, TAG_SIZE_IN_BYTES); // block size = tag size
            BitHelpers.WriteTo(ref ptr, TAG_SIZE_IN_BYTES);
            
            // See the design document for an explanation of the following code.
            var tempKeys = new byte[_symmetricAlgorithmSubkeyLengthInBytes];
            fixed (byte* pbTempKeys = tempKeys)
            {
                byte dummy;
                
                // Derive temporary key for encryption.
                using (var provider = SP800_108_CTR_HMACSHA512Util.CreateEmptyProvider())
                {
                    provider.DeriveKey(
                        pbLabel: &dummy,
                        cbLabel: 0,
                        pbContext: &dummy,
                        cbContext: 0,
                        pbDerivedKey: pbTempKeys,
                        cbDerivedKey: (uint)tempKeys.Length);
                }
                
                // Encrypt a zero-length input string with an all-zero nonce and copy the tag to the return buffer.
                byte* pbNonce = stackalloc byte[(int)NONCE_SIZE_IN_BYTES];
                UnsafeBufferUtil.SecureZeroMemory(pbNonce, NONCE_SIZE_IN_BYTES);
                DoGcmEncrypt(
                    pbKey: pbTempKeys,
                    cbKey: _symmetricAlgorithmSubkeyLengthInBytes,
                    pbNonce: pbNonce,
                    bPlaintextData: &dummy,
                    cbPlaintextData: 0,
                    pbEncryptedData: &dummy,
                    pbTag: ptr);
            }
            
            ptr += TAG_SIZE_IN_BYTES;
            CryptoUtil.Assert(ptr - pbRetVal == retVal.Length, "ptr - pbRetVal == retVal.Length");
        }
        
        // retVal := 
        //   { version || chainingMode || symAlgKeySize || nonceSize || symAlgBlockSize || symAlgTagSize || TAG-of-E("") }.
        return retVal;
    }
            
    public override void Dispose()
    {
        _sp800_108_ctr_hmac_provider.Dispose();        
        // We don't want to dispose of the underlying algorithm instances because they might be reused.
    }                
}

```

###### 2.6.2.1 基类方法- encrypt impl

```c#
internal unsafe sealed class CbcAuthenticatedEncryptor : CngAuthenticatedEncryptorBase
{
    protected override byte[] EncryptImpl(
        byte* pbPlaintext, 
        uint cbPlaintext, 
        byte* pbAdditionalAuthenticatedData, 
        uint cbAdditionalAuthenticatedData, 
        uint cbPreBuffer, 
        uint cbPostBuffer)
    {
        // Allocate a buffer to hold the key modifier, nonce, encrypted data, and tag.
        // In GCM, the encrypted output will be the same length as the plaintext input.
        var retVal = new byte[checked(
            cbPreBuffer + 
            KEY_MODIFIER_SIZE_IN_BYTES + 
            NONCE_SIZE_IN_BYTES + 
            cbPlaintext + 
            TAG_SIZE_IN_BYTES + 
            cbPostBuffer)];
        
        fixed (byte* pbRetVal = retVal)
        {
            // Calculate offsets
            byte* pbKeyModifier = &pbRetVal[cbPreBuffer];
            byte* pbNonce = &pbKeyModifier[KEY_MODIFIER_SIZE_IN_BYTES];
            byte* pbEncryptedData = &pbNonce[NONCE_SIZE_IN_BYTES];
            byte* pbAuthTag = &pbEncryptedData[cbPlaintext];
            
            // Randomly generate the key modifier and nonce
            _genRandom.GenRandom(
                pbKeyModifier, 
                KEY_MODIFIER_SIZE_IN_BYTES + NONCE_SIZE_IN_BYTES);
            
            // At this point, retVal := 
            //   { preBuffer | keyModifier | nonce | _____ | _____ | postBuffer }
            
            // Use the KDF to generate a new symmetric block cipher key
            // We'll need a temporary buffer to hold the symmetric encryption subkey
            byte* pbSymmetricEncryptionSubkey = stackalloc byte[checked((int)_symmetricAlgorithmSubkeyLengthInBytes)];
            try
            {
                _sp800_108_ctr_hmac_provider.DeriveKeyWithContextHeader(
                    pbLabel: pbAdditionalAuthenticatedData,
                    cbLabel: cbAdditionalAuthenticatedData,
                    contextHeader: _contextHeader,
                    pbContext: pbKeyModifier,
                    cbContext: KEY_MODIFIER_SIZE_IN_BYTES,
                    pbDerivedKey: pbSymmetricEncryptionSubkey,
                    cbDerivedKey: _symmetricAlgorithmSubkeyLengthInBytes);
                
                // Perform the encryption operation
                DoGcmEncrypt(
                    pbKey: pbSymmetricEncryptionSubkey,
                    cbKey: _symmetricAlgorithmSubkeyLengthInBytes,
                    pbNonce: pbNonce,
                    pbPlaintextData: pbPlaintext,
                    cbPlaintextData: cbPlaintext,
                    pbEncryptedData: pbEncryptedData,
                    pbTag: pbAuthTag);
                
                // At this point, retVal := 
                //   { preBuffer | keyModifier | nonce | encryptedData | authenticationTag | postBuffer }
                // And we're done!
                return retVal;
            }
            finally
            {
                // The buffer contains key material, so delete it.
                UnsafeBufferUtil.SecureZeroMemory(
                    pbSymmetricEncryptionSubkey, 
                    _symmetricAlgorithmSubkeyLengthInBytes);
            }
        }
    }
    
    // 'pbNonce' must point to a 96-bit buffer.
    // 'pbTag' must point to a 128-bit buffer.
    // 'pbEncryptedData' must point to a buffer the same length as 'pbPlaintextData'.
    private void DoGcmEncrypt(
        byte* pbKey, 
        uint cbKey, 
        byte* pbNonce, 
        byte* pbPlaintextData, 
        uint cbPlaintextData, 
        byte* pbEncryptedData, 
        byte* pbTag)
    {
        BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO authCipherInfo;
        BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO.Init(out authCipherInfo);
        authCipherInfo.pbNonce = pbNonce;
        authCipherInfo.cbNonce = NONCE_SIZE_IN_BYTES;
        authCipherInfo.pbTag = pbTag;
        authCipherInfo.cbTag = TAG_SIZE_IN_BYTES;
        
        using (var keyHandle = _symmetricAlgorithmHandle.GenerateSymmetricKey(pbKey, cbKey))
        {
            uint cbResult;
            var ntstatus = UnsafeNativeMethods.BCryptEncrypt(
                hKey: keyHandle,
                pbInput: pbPlaintextData,
                cbInput: cbPlaintextData,
                pPaddingInfo: &authCipherInfo,
                pbIV: null,
                cbIV: 0,
                pbOutput: pbEncryptedData,
                cbOutput: cbPlaintextData,
                pcbResult: out cbResult,
                dwFlags: 0);
            UnsafeNativeMethods.ThrowExceptionForBCryptStatus(ntstatus);
            CryptoUtil.Assert(cbResult == cbPlaintextData, "cbResult == cbPlaintextData");
        }
    }
}
```

###### 2.6.2.2 基类方法- decrypt impl

```c#
internal unsafe sealed class CbcAuthenticatedEncryptor : CngAuthenticatedEncryptorBase
{
    protected override byte[] DecryptImpl(
        byte* pbCiphertext, 
        uint cbCiphertext, 
        byte* pbAdditionalAuthenticatedData, 
        uint cbAdditionalAuthenticatedData)
    {
        // Argument checking: input must at the absolute minimum contain a key modifier, nonce, and tag
        if (cbCiphertext < KEY_MODIFIER_SIZE_IN_BYTES + NONCE_SIZE_IN_BYTES + TAG_SIZE_IN_BYTES)
        {
            throw Error.CryptCommon_PayloadInvalid();
        }
        
        // Assumption: pbCipherText := 
        //   { keyModifier || nonce || encryptedData || authenticationTag }
        
        var cbPlaintext = checked(
            cbCiphertext - 
            (KEY_MODIFIER_SIZE_IN_BYTES + NONCE_SIZE_IN_BYTES + TAG_SIZE_IN_BYTES));
        
        var retVal = new byte[cbPlaintext];
        fixed (byte* pbRetVal = retVal)
        {
            // Calculate offsets
            byte* pbKeyModifier = pbCiphertext;
            byte* pbNonce = &pbKeyModifier[KEY_MODIFIER_SIZE_IN_BYTES];
            byte* pbEncryptedData = &pbNonce[NONCE_SIZE_IN_BYTES];
            byte* pbAuthTag = &pbEncryptedData[cbPlaintext];
            
            // Use the KDF to recreate the symmetric block cipher key
            // We'll need a temporary buffer to hold the symmetric encryption subkey
            byte* pbSymmetricDecryptionSubkey = stackalloc byte[checked((int)_symmetricAlgorithmSubkeyLengthInBytes)];
            try
            {
                _sp800_108_ctr_hmac_provider.DeriveKeyWithContextHeader(
                    pbLabel: pbAdditionalAuthenticatedData,
                    cbLabel: cbAdditionalAuthenticatedData,
                    contextHeader: _contextHeader,
                    pbContext: pbKeyModifier,
                    cbContext: KEY_MODIFIER_SIZE_IN_BYTES,
                    pbDerivedKey: pbSymmetricDecryptionSubkey,
                    cbDerivedKey: _symmetricAlgorithmSubkeyLengthInBytes);
                
                // Perform the decryption operation
                using (var decryptionSubkeyHandle = _symmetricAlgorithmHandle.GenerateSymmetricKey(
                    		pbSymmetricDecryptionSubkey, 
                    		_symmetricAlgorithmSubkeyLengthInBytes))
                {
                    byte dummy;
                    // CLR doesn't like pinning empty buffers
                    byte* pbPlaintext = (pbRetVal != null) ? pbRetVal : &dummy; 
                    
                    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO authInfo;
                    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO.Init(out authInfo);
                    authInfo.pbNonce = pbNonce;
                    authInfo.cbNonce = NONCE_SIZE_IN_BYTES;
                    authInfo.pbTag = pbAuthTag;
                    authInfo.cbTag = TAG_SIZE_IN_BYTES;
                    
                    // The call to BCryptDecrypt will also validate the authentication tag
                    uint cbDecryptedBytesWritten;
                    var ntstatus = UnsafeNativeMethods.BCryptDecrypt(
                        hKey: decryptionSubkeyHandle,
                        pbInput: pbEncryptedData,
                        cbInput: cbPlaintext,
                        pPaddingInfo: &authInfo,
                        pbIV: null, // IV not used; nonce provided in pPaddingInfo
                        cbIV: 0,
                        pbOutput: pbPlaintext,
                        cbOutput: cbPlaintext,
                        pcbResult: out cbDecryptedBytesWritten,
                        dwFlags: 0);
                    UnsafeNativeMethods.ThrowExceptionForBCryptStatus(ntstatus);
                    CryptoUtil.Assert(
                        cbDecryptedBytesWritten == cbPlaintext, 
                        "cbDecryptedBytesWritten == cbPlaintext");
                    
                    // At this point, retVal := { decryptedPayload }
                    // And we're done!
                    return retVal;
                }
            }
            finally
            {
                // The buffer contains key material, so delete it.
                UnsafeBufferUtil.SecureZeroMemory(
                    pbSymmetricDecryptionSubkey, 
                    _symmetricAlgorithmSubkeyLengthInBytes);
            }
        }
    }
}

```

##### 2.6.3 encryptor factory

```c#
public sealed class CngGcmAuthenticatedEncryptorFactory : IAuthenticatedEncryptorFactory
{
    private readonly ILogger _logger;
    
    public CngGcmAuthenticatedEncryptorFactory(ILoggerFactory loggerFactory)
    {
        
        _logger = loggerFactory.CreateLogger<CngGcmAuthenticatedEncryptorFactory>();
    }

    // 方法- create encryptor
    public IAuthenticatedEncryptor? CreateEncryptorInstance(IKey key)
    {
        // a- 如果 key 的 descriptor 不是 cng gcm authenticated encryptor descriptor，-> 返回 null       
        var descriptor = key.Descriptor as CngGcmAuthenticatedEncryptorDescriptor;        
        if (descriptor == null)
        {
            return null;
        }
        
        Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));        
        return CreateAuthenticatedEncryptorInstance(
            descriptor.MasterKey, 
            descriptor.Configuration);
    }
    
    [SupportedOSPlatform("windows")]
    [return: NotNullIfNotNull("configuration")]
    internal CngGcmAuthenticatedEncryptor? CreateAuthenticatedEncryptorInstance(
        ISecret secret,
        CngGcmAuthenticatedEncryptorConfiguration configuration)
    {
        if (configuration == null)
        {
            return null;
        }
        
        return new CngGcmAuthenticatedEncryptor(
            keyDerivationKey: new Secret(secret),
            symmetricAlgorithmHandle: GetSymmetricBlockCipherAlgorithmHandle(configuration),
            symmetricAlgorithmKeySizeInBytes: (uint)(configuration.EncryptionAlgorithmKeySize / 8));
    }
    
    [SupportedOSPlatform("windows")]
    private BCryptAlgorithmHandle GetSymmetricBlockCipherAlgorithmHandle(CngGcmAuthenticatedEncryptorConfiguration configuration)
    {
        // basic argument checking
        if (String.IsNullOrEmpty(configuration.EncryptionAlgorithm))
        {
            throw Error.Common_PropertyCannotBeNullOrEmpty(nameof(EncryptionAlgorithm));
        }
        if (configuration.EncryptionAlgorithmKeySize < 0)
        {
            throw Error.Common_PropertyMustBeNonNegative(nameof(configuration.EncryptionAlgorithmKeySize));
        }
        
        BCryptAlgorithmHandle? algorithmHandle = null;
        
        _logger.OpeningCNGAlgorithmFromProviderWithChainingModeGCM(
            configuration.EncryptionAlgorithm, 
            configuration.EncryptionAlgorithmProvider);

        // Special-case cached providers
        if (configuration.EncryptionAlgorithmProvider == null)
        {
            if (configuration.EncryptionAlgorithm == Constants.BCRYPT_AES_ALGORITHM) 
            { 
                algorithmHandle = CachedAlgorithmHandles.AES_GCM; 
            }
        }
        
        // Look up the provider dynamically if we couldn't fetch a cached instance
        if (algorithmHandle == null)
        {
            algorithmHandle = BCryptAlgorithmHandle.OpenAlgorithmHandle(
                configuration.EncryptionAlgorithm, 
                configuration.EncryptionAlgorithmProvider);
            
            algorithmHandle.SetChainingMode(Constants.BCRYPT_CHAIN_MODE_GCM);
        }
        
        // make sure we're using a block cipher with an appropriate key size & block size
        CryptoUtil.Assert(
            algorithmHandle.GetCipherBlockLength() == 128 / 8, 
            "GCM requires a block cipher algorithm with a 128-bit block size.");
        
        AlgorithmAssert.IsAllowableSymmetricAlgorithmKeySize(checked((uint)configuration.EncryptionAlgorithmKeySize));
        
        // make sure the provided key length is valid
        algorithmHandle.GetSupportedKeyLengths().EnsureValidKeyLength((uint)configuration.EncryptionAlgorithmKeySize);
        
        // all good!
        return algorithmHandle;
    }
}

```

#### 2.7 managed

##### 2.7.1 descriptor

###### 2.7.1.1 configuration

```c#
public sealed class ManagedAuthenticatedEncryptorConfiguration : 
	AlgorithmConfiguration, 
	IInternalAlgorithmConfiguration
{        
    [ApplyPolicy]
    public Type EncryptionAlgorithmType { get; set; } = typeof(Aes);
           
    [ApplyPolicy]
    public int EncryptionAlgorithmKeySize { get; set; } = 256;
    
    [ApplyPolicy]
    public Type ValidationAlgorithmType { get; set; } = typeof(HMACSHA256);
    
    // 方法- create new descriptor，-> 转到 internal algorithm configuration 的 create 方法（secret random）
    public override IAuthenticatedEncryptorDescriptor CreateNewDescriptor()
    {
        var internalConfiguration = (IInternalAlgorithmConfiguration)this;
        return internalConfiguration.CreateDescriptorFromSecret(Secret.Random(KDK_SIZE_IN_BYTES));
    }
    
    IAuthenticatedEncryptorDescriptor IInternalAlgorithmConfiguration.CreateDescriptorFromSecret(ISecret secret)
    {
        // 创建 managed authenticated encryptor descriptor
        return new ManagedAuthenticatedEncryptorDescriptor(this, secret);
    }
    
    // 方法- validate，用 managed authenticated encryptor factory 验证   
    void IInternalAlgorithmConfiguration.Validate()
    {
        var factory = new ManagedAuthenticatedEncryptorFactory(NullLoggerFactory.Instance);
        // Run a sample payload through an encrypt -> decrypt operation to make sure data round-trips properly.
        using (var encryptor = factory.CreateAuthenticatedEncryptorInstance(Secret.Random(512 / 8), this))
        {
            encryptor.PerformSelfTest();
        }
    }
            
    private static string TypeToFriendlyName(Type type)
    {
        if (type == typeof(Aes))
        {
            return nameof(Aes);
        }
        else if (type == typeof(HMACSHA1))
        {
            return nameof(HMACSHA1);
        }
        else if (type == typeof(HMACSHA256))
        {
            return nameof(HMACSHA256);
        }
        else if (type == typeof(HMACSHA384))
        {
            return nameof(HMACSHA384);
        }
        else if (type == typeof(HMACSHA512))
        {
            return nameof(HMACSHA512);
        }
        else
        {
            return type.AssemblyQualifiedName!;
        }
    }
}

```

###### 2.7.1.2 descriptor deserializer

```c#
public sealed class ManagedAuthenticatedEncryptorDescriptorDeserializer : IAuthenticatedEncryptorDescriptorDeserializer
{    
    public IAuthenticatedEncryptorDescriptor ImportFromXml(XElement element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }
        
        // <descriptor>
        //   <!-- managed implementations -->
        //   <encryption algorithm="..." keyLength="..." />
        //   <validation algorithm="..." />
        //   <masterKey>...</masterKey>
        // </descriptor>

        // 创建 configuration（预结果）
        var configuration = new ManagedAuthenticatedEncryptorConfiguration();
        
        // 从 xelement 解析 "encryption" 节，注入 configuration
        var encryptionElement = element.Element("encryption")!;
        configuration.EncryptionAlgorithmType = FriendlyNameToType((string)encryptionElement.Attribute("algorithm")!);
        configuration.EncryptionAlgorithmKeySize = (int)encryptionElement.Attribute("keyLength")!;
        
        // 从 xelement 解析 "validation" 节，注入 configuration
        var validationElement = element.Element("validation")!;
        configuration.ValidationAlgorithmType = FriendlyNameToType((string)validationElement.Attribute("algorithm")!);
        
        // 从 xelement 解析 "masterKey" 并转换为 secret
        Secret masterKey = ((string)element.Element("masterKey")!).ToSecret();
        
        // 创建 managed authenticated encryptor descriptor
        return new ManagedAuthenticatedEncryptorDescriptor(configuration, masterKey);
    }
            
    private static Type FriendlyNameToType(string typeName)
    {
        if (typeName == nameof(Aes))
        {
            return typeof(Aes);
        }
        else if (typeName == nameof(HMACSHA1))
        {
            return typeof(HMACSHA1);
        }
        else if (typeName == nameof(HMACSHA256))
        {
            return typeof(HMACSHA256);
        }
        else if (typeName == nameof(HMACSHA384))
        {
            return typeof(HMACSHA384);
        }
        else if (typeName == nameof(HMACSHA512))
        {
            return typeof(HMACSHA512);
        }
        else
        {
            return Type.GetType(typeName, throwOnError: true)!;
        }
    }
}

```

###### 2.7.1.3 descriptor

```c#
public sealed class ManagedAuthenticatedEncryptorDescriptor : IAuthenticatedEncryptorDescriptor
{   
    // master key
    internal ISecret MasterKey { get; }    
    // encryptor configuration
    internal ManagedAuthenticatedEncryptorConfiguration Configuration { get; }
    
    public ManagedAuthenticatedEncryptorDescriptor(
        ManagedAuthenticatedEncryptorConfiguration configuration, 
        ISecret masterKey)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }        
        if (masterKey == null)
        {
            throw new ArgumentNullException(nameof(masterKey));
        }
        
        Configuration = configuration;
        MasterKey = masterKey;
    }
            
    // 方法- export to xml
    public XmlSerializedDescriptorInfo ExportToXml()
    {
        // <descriptor>
        //   <!-- managed implementations -->
        //   <encryption algorithm="..." keyLength="..." />
        //   <validation algorithm="..." />
        //   <masterKey>...</masterKey>
        // </descriptor>
        
        // 创建 encryption 节
        var encryptionElement = new XElement(
            "encryption",
            new XAttribute("algorithm", TypeToFriendlyName(Configuration.EncryptionAlgorithmType)),
            new XAttribute("keyLength", Configuration.EncryptionAlgorithmKeySize));
        
        // 创建 validation 节
        var validationElement = new XElement(
            "validation",
            new XAttribute("algorithm", TypeToFriendlyName(Configuration.ValidationAlgorithmType)));
        
        // 创建 xelement，封装 encryption 节、validation 节、master key
        var rootElement = new XElement(
            "descriptor",
            new XComment(" Algorithms provided by specified SymmetricAlgorithm and KeyedHashAlgorithm "),
            encryptionElement,
            validationElement,
            MasterKey.ToMasterKeyElement());
        
        // 创建 xml serialized descriptor info
        return new XmlSerializedDescriptorInfo(
            rootElement, 
            typeof(ManagedAuthenticatedEncryptorDescriptorDeserializer));
    }
       
    private static string TypeToFriendlyName(Type type)
    {
        if (type == typeof(Aes))
        {
            return nameof(Aes);
        }
        else if (type == typeof(HMACSHA1))
        {
            return nameof(HMACSHA1);
        }
        else if (type == typeof(HMACSHA256))
        {
            return nameof(HMACSHA256);
        }
        else if (type == typeof(HMACSHA384))
        {
            return nameof(HMACSHA384);
        }
        else if (type == typeof(HMACSHA512))
        {
            return nameof(HMACSHA512);
        }
        else
        {
            return type.AssemblyQualifiedName!;
        }
    }
}

```

##### 2.7.2 encryptor

```c#
internal unsafe sealed class ManagedAuthenticatedEncryptor : 
	IAuthenticatedEncryptor, 
	IDisposable
{
    // Even when IVs are chosen randomly, CBC is susceptible to IV collisions within a single key. 
    // For a 64-bit block cipher (like 3DES), we'd expect a collision after 2^32 block encryption operations, which a 
    // high-traffic web server might perform in mere hours.
    // AES and other 128-bit block ciphers are less susceptible to this due to the larger IV space, but unfortunately 
    // some organizations require older 64-bit block ciphers. To address the collision issue, we'll feed 128 bits of entropy 
    // to the KDF when performing subkey generation. 
    // This creates >= 192 bits total entropy for each operation, so we shouldn't expect a collision until >= 2^96 operations. 
    // Even 2^80 operations still maintains a <= 2^-32 probability of collision, and this is acceptable for the expected KDK lifetime.
    private const int KEY_MODIFIER_SIZE_IN_BYTES = 128 / 8;
    
    // currently hardcoded to SHA512
    private static readonly Func<byte[], HashAlgorithm> _kdkPrfFactory = key => new HMACSHA512(key); 
    
    private readonly byte[] _contextHeader;
    private readonly IManagedGenRandom _genRandom;
    private readonly Secret _keyDerivationKey;
    private readonly Func<SymmetricAlgorithm> _symmetricAlgorithmFactory;
    private readonly int _symmetricAlgorithmBlockSizeInBytes;
    private readonly int _symmetricAlgorithmSubkeyLengthInBytes;
    private readonly int _validationAlgorithmDigestLengthInBytes;
    private readonly int _validationAlgorithmSubkeyLengthInBytes;
    private readonly Func<KeyedHashAlgorithm> _validationAlgorithmFactory;
    
    public ManagedAuthenticatedEncryptor(
        Secret keyDerivationKey, 
        Func<SymmetricAlgorithm> symmetricAlgorithmFactory, 
        int symmetricAlgorithmKeySizeInBytes, 
        Func<KeyedHashAlgorithm> validationAlgorithmFactory, 
        IManagedGenRandom? genRandom = null)
    {
        // 注入 gen random
        _genRandom = genRandom ?? ManagedGenRandomImpl.Instance;
        // 注入 derivation key (master key)
        _keyDerivationKey = keyDerivationKey;
        
        // Validate that the symmetric algorithm has the properties we require
        using (var symmetricAlgorithm = symmetricAlgorithmFactory())
        {
            _symmetricAlgorithmFactory = symmetricAlgorithmFactory;
            _symmetricAlgorithmBlockSizeInBytes = symmetricAlgorithm.GetBlockSizeInBytes();
            _symmetricAlgorithmSubkeyLengthInBytes = symmetricAlgorithmKeySizeInBytes;
        }
        
        // Validate that the MAC algorithm has the properties we require
        using (var validationAlgorithm = validationAlgorithmFactory())
        {
            _validationAlgorithmFactory = validationAlgorithmFactory;
            _validationAlgorithmDigestLengthInBytes = validationAlgorithm.GetDigestSizeInBytes();
            // for simplicity we'll generate MAC subkeys with a length equal to the digest length
            _validationAlgorithmSubkeyLengthInBytes = _validationAlgorithmDigestLengthInBytes; 
        }
        
        // Argument checking on the algorithms and lengths passed in to us
        AlgorithmAssert.IsAllowableSymmetricAlgorithmBlockSize(checked((uint)_symmetricAlgorithmBlockSizeInBytes * 8));
        AlgorithmAssert.IsAllowableSymmetricAlgorithmKeySize(checked((uint)_symmetricAlgorithmSubkeyLengthInBytes * 8));
        AlgorithmAssert.IsAllowableValidationAlgorithmDigestSize(checked((uint)_validationAlgorithmDigestLengthInBytes * 8));
        
        _contextHeader = CreateContextHeader();
    }
    
    private byte[] CreateContextHeader()
    {
        var EMPTY_ARRAY = Array.Empty<byte>();
        var EMPTY_ARRAY_SEGMENT = new ArraySegment<byte>(EMPTY_ARRAY);
        
        var retVal = new byte[checked(
            1 /* KDF alg */
            + 1 /* chaining mode */
            + sizeof(uint) /* sym alg key size */
            + sizeof(uint) /* sym alg block size */
            + sizeof(uint) /* hmac alg key size */
            + sizeof(uint) /* hmac alg digest size */
            + _symmetricAlgorithmBlockSizeInBytes /* ciphertext of encrypted empty string */
            + _validationAlgorithmDigestLengthInBytes /* digest of HMACed empty string */)];
        
        var idx = 0;
        
        // First is the two-byte header
        retVal[idx++] = 0; // 0x00 = SP800-108 CTR KDF w/ HMACSHA512 PRF
        retVal[idx++] = 0; // 0x00 = CBC encryption + HMAC authentication
        
        // Next is information about the symmetric algorithm (key size followed by block size)
        BitHelpers.WriteTo(retVal, ref idx, _symmetricAlgorithmSubkeyLengthInBytes);
        BitHelpers.WriteTo(retVal, ref idx, _symmetricAlgorithmBlockSizeInBytes);
        
        // Next is information about the keyed hash algorithm (key size followed by digest size)
        BitHelpers.WriteTo(retVal, ref idx, _validationAlgorithmSubkeyLengthInBytes);
        BitHelpers.WriteTo(retVal, ref idx, _validationAlgorithmDigestLengthInBytes);
        
        // See the design document for an explanation of the following code.
        var tempKeys = new byte[_symmetricAlgorithmSubkeyLengthInBytes + _validationAlgorithmSubkeyLengthInBytes];
        ManagedSP800_108_CTR_HMACSHA512.DeriveKeys(
            kdk: EMPTY_ARRAY,
            label: EMPTY_ARRAY_SEGMENT,
            context: EMPTY_ARRAY_SEGMENT,
            prfFactory: _kdkPrfFactory,
            output: new ArraySegment<byte>(tempKeys));
        
        // At this point, tempKeys := { K_E || K_H }.
                
        // Encrypt a zero-length input string with an all-zero IV and copy the ciphertext to the return buffer.
        using (var symmetricAlg = CreateSymmetricAlgorithm())
        {
            using (var cryptoTransform = symmetricAlg.CreateEncryptor(
                rgbKey: new ArraySegment<byte>(
                    tempKeys, 
                    0, 
                    _symmetricAlgorithmSubkeyLengthInBytes).AsStandaloneArray(),
                rgbIV: new byte[_symmetricAlgorithmBlockSizeInBytes]))
            {
                var ciphertext = cryptoTransform.TransformFinalBlock(EMPTY_ARRAY, 0, 0);
                CryptoUtil.Assert(
                    ciphertext != null && ciphertext.Length == _symmetricAlgorithmBlockSizeInBytes, 
                    "ciphertext != null && ciphertext.Length == _symmetricAlgorithmBlockSizeInBytes");
                Buffer.BlockCopy(ciphertext, 0, retVal, idx, ciphertext.Length);
            }
        }
        
        idx += _symmetricAlgorithmBlockSizeInBytes;
        
        // MAC a zero-length input string and copy the digest to the return buffer.
        using (var hashAlg = CreateValidationAlgorithm(new ArraySegment<byte>(
            tempKeys, 
            _symmetricAlgorithmSubkeyLengthInBytes, 
            _validationAlgorithmSubkeyLengthInBytes).AsStandaloneArray()))
        {
            var digest = hashAlg.ComputeHash(EMPTY_ARRAY);
            CryptoUtil.Assert(
                digest != null && digest.Length == _validationAlgorithmDigestLengthInBytes, 
                "digest != null && digest.Length == _validationAlgorithmDigestLengthInBytes");
            Buffer.BlockCopy(
                digest, 
                0, 
                retVal, 
                idx, 
                digest.Length);
        }
        
        idx += _validationAlgorithmDigestLengthInBytes;
        CryptoUtil.Assert(idx == retVal.Length, "idx == retVal.Length");
        
        // retVal := 
        //   { version || chainingMode || symAlgKeySize || symAlgBlockSize || macAlgKeySize || macAlgDigestSize || E("") || MAC("") }.
        return retVal;
    }
    
    private SymmetricAlgorithm CreateSymmetricAlgorithm()
    {
        var retVal = _symmetricAlgorithmFactory();
        CryptoUtil.Assert(retVal != null, "retVal != null");
        
        retVal.Mode = CipherMode.CBC;
        retVal.Padding = PaddingMode.PKCS7;
        return retVal;
    }
    
    private KeyedHashAlgorithm CreateValidationAlgorithm(byte[] key)
    {
        var retVal = _validationAlgorithmFactory();
        CryptoUtil.Assert(retVal != null, "retVal != null");
        
        retVal.Key = key;
        return retVal;
    }
            
    public void Dispose()
    {
        _keyDerivationKey.Dispose();
    }        
}

```

###### 2.7.2.1 方法- encrypt 

```c#
internal unsafe sealed class ManagedAuthenticatedEncryptor 
{
    public byte[] Encrypt(
        ArraySegment<byte> plaintext, 
        ArraySegment<byte> additionalAuthenticatedData)
    {
        // 验证
        plaintext.Validate();
        additionalAuthenticatedData.Validate();
        
        try
        {
            var outputStream = new MemoryStream();
            
            // Step 1: Generate a random key modifier and IV for this operation.
            // Both will be equal to the block size of the block cipher algorithm.
            
            var keyModifier = _genRandom.GenRandom(KEY_MODIFIER_SIZE_IN_BYTES);
            var iv = _genRandom.GenRandom(_symmetricAlgorithmBlockSizeInBytes);
            
            // Step 2: Copy the key modifier and the IV to the output stream since they'll act as a header.
            
            outputStream.Write(keyModifier, 0, keyModifier.Length);
            outputStream.Write(iv, 0, iv.Length);
            
            // At this point, outputStream := { keyModifier || IV }.
            
            // Step 3: Decrypt the KDK, and use it to generate new encryption and HMAC keys.
            // We pin all unencrypted keys to limit their exposure via GC relocation.
            
            var decryptedKdk = new byte[_keyDerivationKey.Length];
            var encryptionSubkey = new byte[_symmetricAlgorithmSubkeyLengthInBytes];
            var validationSubkey = new byte[_validationAlgorithmSubkeyLengthInBytes];
            var derivedKeysBuffer = new byte[checked(encryptionSubkey.Length + validationSubkey.Length)];
            
            fixed (byte* __unused__1 = decryptedKdk)
                fixed (byte* __unused__2 = encryptionSubkey)
                fixed (byte* __unused__3 = validationSubkey)
                fixed (byte* __unused__4 = derivedKeysBuffer)
            {
                try
                {
                    _keyDerivationKey.WriteSecretIntoBuffer(new ArraySegment<byte>(decryptedKdk));
                    ManagedSP800_108_CTR_HMACSHA512.DeriveKeysWithContextHeader(
                        kdk: decryptedKdk,
                        label: additionalAuthenticatedData,
                        contextHeader: _contextHeader,
                        context: new ArraySegment<byte>(keyModifier),
                        prfFactory: _kdkPrfFactory,
                        output: new ArraySegment<byte>(derivedKeysBuffer));
                    
                    Buffer.BlockCopy(derivedKeysBuffer, 0, encryptionSubkey, 0, encryptionSubkey.Length);
                    Buffer.BlockCopy(derivedKeysBuffer, encryptionSubkey.Length, validationSubkey, 0, validationSubkey.Length);
                    
                    // Step 4: Perform the encryption operation.
                    
                    using (var symmetricAlgorithm = CreateSymmetricAlgorithm())
                    using (var cryptoTransform = symmetricAlgorithm.CreateEncryptor(encryptionSubkey, iv))
                    using (var cryptoStream = new CryptoStream(outputStream, cryptoTransform, CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(plaintext.Array!, plaintext.Offset, plaintext.Count);
                        cryptoStream.FlushFinalBlock();
                        
                        // At this point, outputStream := { keyModifier || IV || ciphertext }
                        
                        // Step 5: Calculate the digest over the IV and ciphertext.
                        // We don't need to calculate the digest over the key modifier since that
                        // value has already been mixed into the KDF used to generate the MAC key.
                        
                        using (var validationAlgorithm = CreateValidationAlgorithm(validationSubkey))
                        {
                            // As an optimization, avoid duplicating the underlying buffer
                            var underlyingBuffer = outputStream.GetBuffer();
                            var mac = validationAlgorithm.ComputeHash(
                                underlyingBuffer, 
                                KEY_MODIFIER_SIZE_IN_BYTES, 
                                checked((int)outputStream.Length - KEY_MODIFIER_SIZE_IN_BYTES));
                            outputStream.Write(mac, 0, mac.Length);
                            
                            // At this point, outputStream := { keyModifier || IV || ciphertext || MAC(IV || ciphertext) }
                            // And we're done!
                            return outputStream.ToArray();
                        }
                    }
                }
                finally
                {
                    // delete since these contain secret material
                    Array.Clear(decryptedKdk, 0, decryptedKdk.Length);
                    Array.Clear(encryptionSubkey, 0, encryptionSubkey.Length);
                    Array.Clear(validationSubkey, 0, validationSubkey.Length);
                    Array.Clear(derivedKeysBuffer, 0, derivedKeysBuffer.Length);
                }
            }
        }
        catch (Exception ex) when (ex.RequiresHomogenization())
        {
            // Homogenize all exceptions to CryptographicException.
            throw Error.CryptCommon_GenericError(ex);
        }
    }
}

```

###### 2.7.2.2 方法- decrypt

```c#
internal unsafe sealed class ManagedAuthenticatedEncryptor 
{
    public byte[] Decrypt(
        ArraySegment<byte> protectedPayload, 
        ArraySegment<byte> additionalAuthenticatedData)
    {
        protectedPayload.Validate();
        additionalAuthenticatedData.Validate();
        
        // Argument checking - input must at the absolute minimum contain a key modifier, IV, and MAC
        if (protectedPayload.Count < checked(
                KEY_MODIFIER_SIZE_IN_BYTES + 
                _symmetricAlgorithmBlockSizeInBytes + 
                _validationAlgorithmDigestLengthInBytes))
        {
            throw Error.CryptCommon_PayloadInvalid();
        }
        
        // Assumption: protectedPayload := { keyModifier | IV | encryptedData | MAC(IV | encryptedPayload) }
        
        try
        {
            // Step 1: Extract the key modifier and IV from the payload.
            
            int keyModifierOffset; // position in protectedPayload.Array where key modifier begins
            int ivOffset; // position in protectedPayload.Array where key modifier ends / IV begins
            int ciphertextOffset; // position in protectedPayload.Array where IV ends / ciphertext begins
            int macOffset; // position in protectedPayload.Array where ciphertext ends / MAC begins
            int eofOffset; // position in protectedPayload.Array where MAC ends
            
            checked
            {
                keyModifierOffset = protectedPayload.Offset;
                ivOffset = keyModifierOffset + KEY_MODIFIER_SIZE_IN_BYTES;
                ciphertextOffset = ivOffset + _symmetricAlgorithmBlockSizeInBytes;
            }
            
            ArraySegment<byte> keyModifier = new ArraySegment<byte>(
                protectedPayload.Array!, 
                keyModifierOffset, 
                ivOffset - keyModifierOffset);
            var iv = new byte[_symmetricAlgorithmBlockSizeInBytes];
            Buffer.BlockCopy(protectedPayload.Array!, ivOffset, iv, 0, iv.Length);
            
            // Step 2: Decrypt the KDK and use it to restore the original encryption and MAC keys.
            // We pin all unencrypted keys to limit their exposure via GC relocation.
                        
            var decryptedKdk = new byte[_keyDerivationKey.Length];
            var decryptionSubkey = new byte[_symmetricAlgorithmSubkeyLengthInBytes];
            var validationSubkey = new byte[_validationAlgorithmSubkeyLengthInBytes];
            var derivedKeysBuffer = new byte[checked(decryptionSubkey.Length + validationSubkey.Length)];
            
            fixed (byte* __unused__1 = decryptedKdk)
            fixed (byte* __unused__2 = decryptionSubkey)
            fixed (byte* __unused__3 = validationSubkey)
            fixed (byte* __unused__4 = derivedKeysBuffer)
            {
                try
                {
                    _keyDerivationKey.WriteSecretIntoBuffer(new ArraySegment<byte>(decryptedKdk));
                    ManagedSP800_108_CTR_HMACSHA512.DeriveKeysWithContextHeader(
                        kdk: decryptedKdk,
                        label: additionalAuthenticatedData,
                        contextHeader: _contextHeader,
                        context: keyModifier,
                        prfFactory: _kdkPrfFactory,
                        output: new ArraySegment<byte>(derivedKeysBuffer));
                    
                    Buffer.BlockCopy(derivedKeysBuffer, 0, decryptionSubkey, 0, decryptionSubkey.Length);
                    Buffer.BlockCopy(derivedKeysBuffer, decryptionSubkey.Length, validationSubkey, 0, validationSubkey.Length);
                    
                    // Step 3: Calculate the correct MAC for this payload.
                    // correctHash := MAC(IV || ciphertext)
                    byte[] correctHash;
                    
                    using (var hashAlgorithm = CreateValidationAlgorithm(validationSubkey))
                    {
                        checked
                        {
                            eofOffset = protectedPayload.Offset + protectedPayload.Count;
                            macOffset = eofOffset - _validationAlgorithmDigestLengthInBytes;
                        }
                        
                        correctHash = hashAlgorithm.ComputeHash(
                            protectedPayload.Array!, 
                            ivOffset, 
                            macOffset - ivOffset);
                    }
                    
                    // Step 4: Validate the MAC provided as part of the payload.
                    if (!CryptoUtil.TimeConstantBuffersAreEqual(
                        	correctHash, 
                        	0, 
                        	correctHash.Length, 
                        	protectedPayload.Array!, 
                        	macOffset, 
                        	eofOffset - macOffset))
                    {
                        throw Error.CryptCommon_PayloadInvalid(); // integrity check failure
                    }
                    
                    // Step 5: Decipher the ciphertext and return it to the caller.
                    
                    using (var symmetricAlgorithm = CreateSymmetricAlgorithm())
                    using (var cryptoTransform = symmetricAlgorithm.CreateDecryptor(decryptionSubkey, iv))
                    {
                        var outputStream = new MemoryStream();
                        using (var cryptoStream = new CryptoStream(outputStream, cryptoTransform, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(protectedPayload.Array!, ciphertextOffset, macOffset - ciphertextOffset);
                            cryptoStream.FlushFinalBlock();
                            
                            // At this point, outputStream := { plaintext }, and we're done!
                            return outputStream.ToArray();
                        }
                    }
                }
                finally
                {
                    // delete since these contain secret material
                    Array.Clear(decryptedKdk, 0, decryptedKdk.Length);
                    Array.Clear(decryptionSubkey, 0, decryptionSubkey.Length);
                    Array.Clear(validationSubkey, 0, validationSubkey.Length);
                    Array.Clear(derivedKeysBuffer, 0, derivedKeysBuffer.Length);
                }
            }
        }
        catch (Exception ex) when (ex.RequiresHomogenization())
        {
            // Homogenize all exceptions to CryptographicException.
            throw Error.CryptCommon_GenericError(ex);
        }
    }
}

```

###### 2.7.2.3 managed gen random

```c#
// 接口
internal interface IManagedGenRandom
{
    byte[] GenRandom(int numBytes);
}

// 实现
internal unsafe sealed class ManagedGenRandomImpl : IManagedGenRandom
{
#if NETSTANDARD2_0 || NET461
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
#endif
    public static readonly ManagedGenRandomImpl Instance = new ManagedGenRandomImpl();
    
    private ManagedGenRandomImpl()
    {
    }
    
    public byte[] GenRandom(int numBytes)
    {
        var bytes = new byte[numBytes];
#if NETSTANDARD2_0 || NET461
        _rng.GetBytes(bytes);
#else
        RandomNumberGenerator.Fill(bytes);
#endif
        return bytes;
    }
}

```

###### 2.7.2.4 hash algorithm extensions

```c#
internal static class HashAlgorithmExtensions
{
    public static int GetDigestSizeInBytes(this HashAlgorithm hashAlgorithm)
    {
        var hashSizeInBits = hashAlgorithm.HashSize;
        CryptoUtil.Assert(
            hashSizeInBits >= 0 && hashSizeInBits % 8 == 0, 
            "hashSizeInBits >= 0 && hashSizeInBits % 8 == 0");
        
        return hashSizeInBits / 8;
    }
}

```

###### 2.7.2.5 symmetric algorithm extensions

```c#
internal static class SymmetricAlgorithmExtensions
{
    public static int GetBlockSizeInBytes(this SymmetricAlgorithm symmetricAlgorithm)
    {
        var blockSizeInBits = symmetricAlgorithm.BlockSize;
        CryptoUtil.Assert(
            blockSizeInBits >= 0 && blockSizeInBits % 8 == 0, 
            "blockSizeInBits >= 0 && blockSizeInBits % 8 == 0");
        
        return blockSizeInBits / 8;
    }
}

```

##### 2.7.3 encryptor factory

```c#
 public sealed class ManagedAuthenticatedEncryptorFactory : IAuthenticatedEncryptorFactory
 {
     private readonly ILogger _logger;
     
     public ManagedAuthenticatedEncryptorFactory(ILoggerFactory loggerFactory)
     {
         _logger = loggerFactory.CreateLogger<ManagedAuthenticatedEncryptorFactory>();
     }
     
     // 方法- create encryptor
     public IAuthenticatedEncryptor? CreateEncryptorInstance(IKey key)
     {
         // a- 如果 key 的 descriptor 不是 managed authenticated encryptor descriptor，-> 返回 null         
         if (key.Descriptor is not ManagedAuthenticatedEncryptorDescriptor descriptor)
         {
             return null;
         }
                  
         return CreateAuthenticatedEncryptorInstance(descriptor.MasterKey, descriptor.Configuration);
     }
     
     [return: NotNullIfNotNull("configuration")]
     internal ManagedAuthenticatedEncryptor? CreateAuthenticatedEncryptorInstance(
         ISecret secret,
         ManagedAuthenticatedEncryptorConfiguration? configuration)
     {
         if (configuration == null)
         {
             return null;
         }
         
         // b- 创建 managed authenticated encryptor
         return new ManagedAuthenticatedEncryptor(
             keyDerivationKey: new Secret(secret),
             // 1-
             symmetricAlgorithmFactory: GetSymmetricBlockCipherAlgorithmFactory(configuration),
             symmetricAlgorithmKeySizeInBytes: configuration.EncryptionAlgorithmKeySize / 8,
             // 2-
             validationAlgorithmFactory: GetKeyedHashAlgorithmFactory(configuration));
     }
     
     // 2-
     private Func<KeyedHashAlgorithm> GetKeyedHashAlgorithmFactory(ManagedAuthenticatedEncryptorConfiguration configuration)
     {
         // basic argument checking
         if (configuration.ValidationAlgorithmType == null)
         {
             throw Error.Common_PropertyCannotBeNullOrEmpty(nameof(configuration.ValidationAlgorithmType));
         }
         
         _logger.UsingManagedKeyedHashAlgorithm(configuration.ValidationAlgorithmType.FullName!);
         if (configuration.ValidationAlgorithmType == typeof(HMACSHA256))
         {
             return () => new HMACSHA256();
         }
         else if (configuration.ValidationAlgorithmType == typeof(HMACSHA512))
         {
             return () => new HMACSHA512();
         }
         else
         {
             return AlgorithmActivator.CreateFactory<KeyedHashAlgorithm>(configuration.ValidationAlgorithmType);
         }
     }
     // 1-
     private Func<SymmetricAlgorithm> GetSymmetricBlockCipherAlgorithmFactory(ManagedAuthenticatedEncryptorConfiguration configuration)
     {
         // basic argument checking
         if (configuration.EncryptionAlgorithmType == null)
         {
             throw Error.Common_PropertyCannotBeNullOrEmpty(nameof(configuration.EncryptionAlgorithmType));
         }
         typeof(SymmetricAlgorithm).AssertIsAssignableFrom(configuration.EncryptionAlgorithmType);
         if (configuration.EncryptionAlgorithmKeySize < 0)
         {
             throw Error.Common_PropertyMustBeNonNegative(nameof(configuration.EncryptionAlgorithmKeySize));
         }
         
         _logger.UsingManagedSymmetricAlgorithm(configuration.EncryptionAlgorithmType.FullName!);
         
         if (configuration.EncryptionAlgorithmType == typeof(Aes))
         {
             return Aes.Create;
         }
         else
         {
             return AlgorithmActivator.CreateFactory<SymmetricAlgorithm>(configuration.EncryptionAlgorithmType);
         }
     }
         
     private static class AlgorithmActivator
     {        
         public static Func<T> CreateFactory<T>(Type implementation)
         {
             return ((IActivator<T>)Activator.CreateInstance(
                 typeof(AlgorithmActivatorCore<>).MakeGenericType(implementation))!).Creator;
         }
         
         private interface IActivator<out T>
         {
             Func<T> Creator { get; }
         }
         
         private class AlgorithmActivatorCore<T> : IActivator<T> where T : new()
         {
             public Func<T> Creator { get; } = Activator.CreateInstance<T>;
         }
     }
 }

```

#### 2.8 aes gcm

##### 2.8.1 aes gmc encryptor

```c#
internal unsafe sealed class AesGcmAuthenticatedEncryptor : 
	IOptimizedAuthenticatedEncryptor, 
	IDisposable
{
    // Having a key modifier ensures with overwhelming probability that no two encryption operations will ever derive 
    // the same (encryption subkey, MAC subkey) pair. This limits an attacker's ability to mount a key-dependent chosen 
    // ciphertext attack. See also the class-level comment on CngGcmAuthenticatedEncryptor for how this is used to 
    // overcome GCM's IV limitations.
    private const int KEY_MODIFIER_SIZE_IN_BYTES = 128 / 8;    
    private const int NONCE_SIZE_IN_BYTES = 96 / 8; 		// GCM has a fixed 96-bit IV
    private const int TAG_SIZE_IN_BYTES = 128 / 8; 			// we're hardcoding a 128-bit authentication tag size
    
    // See CngGcmAuthenticatedEncryptor.CreateContextHeader for how these were precomputed
    
    // 128 "00-01-00-00-00-10-00-00-00-0C-00-00-00-10-00-00-00-10-95-7C-50-FF-69-2E-38-8B-9A-D5-C7-68-9E-4B-9E-2B"
    private static readonly byte[] AES_128_GCM_Header = new byte[] 
    { 
        0x00, 0x01, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 
        0x00, 0x0C, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 
        0x00, 0x10, 0x95, 0x7C, 0x50, 0xFF, 0x69, 0x2E, 
        0x38, 0x8B, 0x9A, 0xD5, 0xC7, 0x68, 0x9E, 0x4B, 
        0x9E, 0x2B 
    };
    
    // 192 "00-01-00-00-00-18-00-00-00-0C-00-00-00-10-00-00-00-10-0D-AA-01-3A-95-0A-DA-2B-79-8F-5F-F2-72-FA-D3-63"
    private static readonly byte[] AES_192_GCM_Header = new byte[] 
    {
        0x00, 0x01, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 
        0x00, 0x0C, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 
        0x00, 0x10, 0x0D, 0xAA, 0x01, 0x3A, 0x95, 0x0A, 
        0xDA, 0x2B, 0x79, 0x8F, 0x5F, 0xF2, 0x72, 0xFA, 
        0xD3, 0x63
    };
    
    // 256 00-01-00-00-00-20-00-00-00-0C-00-00-00-10-00-00-00-10-E7-DC-CE-66-DF-85-5A-32-3A-6B-B7-BD-7A-59-BE-45
    private static readonly byte[] AES_256_GCM_Header = new byte[] 
    {
        0x00, 0x01, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 
        0x00, 0x0C, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 
        0x00, 0x10, 0xE7, 0xDC, 0xCE, 0x66, 0xDF, 0x85, 
        0x5A, 0x32, 0x3A, 0x6B, 0xB7, 0xBD, 0x7A, 0x59, 
        0xBE, 0x45 
    };
    
    // currently hardcoded to SHA512
    private static readonly Func<byte[], HashAlgorithm> _kdkPrfFactory = key => new HMACSHA512(key); 
    
    private readonly byte[] _contextHeader;
    
    private readonly Secret _keyDerivationKey;
    private readonly int _derivedkeySizeInBytes;
    private readonly IManagedGenRandom _genRandom;
    
    public AesGcmAuthenticatedEncryptor(
        ISecret keyDerivationKey, 
        int derivedKeySizeInBytes, 
        IManagedGenRandom? genRandom = null)
    {
        // 注入 derivation key (master key)
        _keyDerivationKey = new Secret(keyDerivationKey);
        _derivedkeySizeInBytes = derivedKeySizeInBytes;
        
        switch (_derivedkeySizeInBytes)
        {
            case 16:
                _contextHeader = AES_128_GCM_Header;
                break;
            case 24:
                _contextHeader = AES_192_GCM_Header;
                break;
            case 32:
                _contextHeader = AES_256_GCM_Header;
                break;
            default:
                // should never happen
                throw CryptoUtil.Fail("Unexpected AES key size in bytes only support 16, 24, 32."); 
        }
        
        // 注入 gen random
        _genRandom = genRandom ?? ManagedGenRandomImpl.Instance;
    }        
            
    public void Dispose()
    {
        _keyDerivationKey.Dispose();
    }
}

```

###### 2.8.1.1 方法- encrypt

```c#
internal unsafe sealed class AesGcmAuthenticatedEncryptor 
{
    public byte[] Encrypt(
        ArraySegment<byte> plaintext, 
        ArraySegment<byte> additionalAuthenticatedData) => 
        	Encrypt(plaintext, additionalAuthenticatedData, 0, 0);
    
    public byte[] Encrypt(
        ArraySegment<byte> plaintext, 
        ArraySegment<byte> additionalAuthenticatedData, 
        uint preBufferSize, 
        uint postBufferSize)
    {
        plaintext.Validate();
        additionalAuthenticatedData.Validate();
        
        try
        {
            // Allocate a buffer to hold the key modifier, nonce, encrypted data, and tag.
            // In GCM, the encrypted output will be the same length as the plaintext input.
            var retVal = new byte[checked(
                preBufferSize + 
                KEY_MODIFIER_SIZE_IN_BYTES + 
                NONCE_SIZE_IN_BYTES + 
                plaintext.Count + 
                TAG_SIZE_IN_BYTES + 
                postBufferSize)];
            
            int keyModifierOffset; // position in ciphertext.Array where key modifier begins
            int nonceOffset; // position in ciphertext.Array where key modifier ends / nonce begins
            int encryptedDataOffset; // position in ciphertext.Array where nonce ends / encryptedData begins
            int tagOffset; // position in ciphertext.Array where encrypted data ends
            
            checked
            {
                keyModifierOffset = plaintext.Offset + (int)preBufferSize;
                nonceOffset = keyModifierOffset + KEY_MODIFIER_SIZE_IN_BYTES;
                encryptedDataOffset = nonceOffset + NONCE_SIZE_IN_BYTES;
                tagOffset = encryptedDataOffset + plaintext.Count;
            }
            
            // Randomly generate the key modifier and nonce
            var keyModifier = _genRandom.GenRandom(KEY_MODIFIER_SIZE_IN_BYTES);
            var nonceBytes = _genRandom.GenRandom(NONCE_SIZE_IN_BYTES);
            
            Buffer.BlockCopy(keyModifier, 0, retVal, (int)preBufferSize, keyModifier.Length);
            Buffer.BlockCopy(nonceBytes, 0, retVal, (int)preBufferSize + keyModifier.Length, nonceBytes.Length);
            
            // At this point, retVal := { preBuffer | keyModifier | nonce | _____ | _____ | postBuffer }
            
            // Use the KDF to generate a new symmetric block cipher key
            // We'll need a temporary buffer to hold the symmetric encryption subkey
            var decryptedKdk = new byte[_keyDerivationKey.Length];
            var derivedKey = new byte[_derivedkeySizeInBytes];
            fixed (byte* __unused__1 = decryptedKdk)
                fixed (byte* __unused__2 = derivedKey)
            {
                try
                {
                    _keyDerivationKey.WriteSecretIntoBuffer(new ArraySegment<byte>(decryptedKdk));
                    ManagedSP800_108_CTR_HMACSHA512.DeriveKeysWithContextHeader(
                        kdk: decryptedKdk,
                        label: additionalAuthenticatedData,
                        contextHeader: _contextHeader,
                        context: keyModifier,
                        prfFactory: _kdkPrfFactory,
                        output: new ArraySegment<byte>(derivedKey));
                    
                    // do gcm
                    var nonce = new Span<byte>(retVal, nonceOffset, NONCE_SIZE_IN_BYTES);
                    var tag = new Span<byte>(retVal, tagOffset, TAG_SIZE_IN_BYTES);
                    var encrypted = new Span<byte>(retVal, encryptedDataOffset, plaintext.Count);
                    using var aes = new AesGcm(derivedKey);
                    aes.Encrypt(nonce, plaintext, encrypted, tag);
                    
                    // At this point, retVal := 
                    //   { preBuffer | keyModifier | nonce | encryptedData | authenticationTag | postBuffer }
                    // And we're done!
                    return retVal;
                }
                finally
                {
                    // delete since these contain secret material
                    Array.Clear(decryptedKdk, 0, decryptedKdk.Length);
                    Array.Clear(derivedKey, 0, derivedKey.Length);
                }
            }
        }
        catch (Exception ex) when (ex.RequiresHomogenization())
        {
            // Homogenize all exceptions to CryptographicException.
            throw Error.CryptCommon_GenericError(ex);
        }
    }        
}

```

###### 2.8.1.2 方法- decrypt

```c#
internal unsafe sealed class AesGcmAuthenticatedEncryptor 
{
    public byte[] Decrypt(
        ArraySegment<byte> ciphertext, 
        ArraySegment<byte> additionalAuthenticatedData)
    {
        ciphertext.Validate();
        additionalAuthenticatedData.Validate();
        
        // Argument checking: input must at the absolute minimum contain a key modifier, nonce, and tag
        if (ciphertext.Count < KEY_MODIFIER_SIZE_IN_BYTES + NONCE_SIZE_IN_BYTES + TAG_SIZE_IN_BYTES)
        {
            throw Error.CryptCommon_PayloadInvalid();
        }
        
        // Assumption: pbCipherText := { keyModifier || nonce || encryptedData || authenticationTag }
        var plaintextBytes = ciphertext.Count - (KEY_MODIFIER_SIZE_IN_BYTES + NONCE_SIZE_IN_BYTES + TAG_SIZE_IN_BYTES);
        var plaintext = new byte[plaintextBytes];
        
        try
        {
            // Step 1: Extract the key modifier from the payload.
            
            int keyModifierOffset; 		// position in ciphertext.Array where key modifier begins
            int nonceOffset; 			// position in ciphertext.Array where key modifier ends / nonce begins
            int encryptedDataOffset; 	// position in ciphertext.Array where nonce ends / encryptedData begins
            int tagOffset; 				// position in ciphertext.Array where encrypted data ends
            
            checked
            {
                keyModifierOffset = ciphertext.Offset;
                nonceOffset = keyModifierOffset + KEY_MODIFIER_SIZE_IN_BYTES;
                encryptedDataOffset = nonceOffset + NONCE_SIZE_IN_BYTES;
                tagOffset = encryptedDataOffset + plaintextBytes;
            }
            
            var keyModifier = new ArraySegment<byte>(ciphertext.Array!, keyModifierOffset, KEY_MODIFIER_SIZE_IN_BYTES);
            
            // Step 2: Decrypt the KDK and use it to restore the original encryption and MAC keys.
            // We pin all unencrypted keys to limit their exposure via GC relocation.
            
            var decryptedKdk = new byte[_keyDerivationKey.Length];
            var derivedKey = new byte[_derivedkeySizeInBytes];
            
            fixed (byte* __unused__1 = decryptedKdk)
                fixed (byte* __unused__2 = derivedKey)
            {
                try
                {
                    _keyDerivationKey.WriteSecretIntoBuffer(new ArraySegment<byte>(decryptedKdk));
                    ManagedSP800_108_CTR_HMACSHA512.DeriveKeysWithContextHeader(
                        kdk: decryptedKdk,
                        label: additionalAuthenticatedData,
                        contextHeader: _contextHeader,
                        context: keyModifier,
                        prfFactory: _kdkPrfFactory,
                        output: new ArraySegment<byte>(derivedKey));
                    
                    // Perform the decryption operation
                    var nonce = new Span<byte>(ciphertext.Array, nonceOffset, NONCE_SIZE_IN_BYTES);
                    var tag = new Span<byte>(ciphertext.Array, tagOffset, TAG_SIZE_IN_BYTES);
                    var encrypted = new Span<byte>(ciphertext.Array, encryptedDataOffset, plaintextBytes);
                    using var aes = new AesGcm(derivedKey);
                    aes.Decrypt(nonce, encrypted, tag, plaintext);
                    return plaintext;
                }
                finally
                {
                    // delete since these contain secret material
                    Array.Clear(decryptedKdk, 0, decryptedKdk.Length);
                    Array.Clear(derivedKey, 0, derivedKey.Length);
                }
            }
        }
        catch (Exception ex) when (ex.RequiresHomogenization())
        {
            // Homogenize all exceptions to CryptographicException.
            throw Error.CryptCommon_GenericError(ex);
        }
    }
}

```


### 3. about key

#### 3.1 secret 

* secure memory for value

```c#
// 接口
public interface ISecret : IDisposable
{   
    int Length { get; }    
    // The buffer size must exactly match the length of the secret value.    
    void WriteSecretIntoBuffer(ArraySegment<byte> buffer);
}

// 实现
public unsafe sealed class Secret : IDisposable, ISecret
{
    // from wincrypt.h
    private const uint CRYPTPROTECTMEMORY_BLOCK_SIZE = 16;
    
    private readonly SecureLocalAllocHandle _localAllocHandle;
    private readonly uint _plaintextLength;
    
    public int Length
    {
        get
        {
            // ctor guarantees the length fits into a signed int
            return (int)_plaintextLength; 
        }
    }
    
    /* 创建 secret by ctor */
    public Secret(ArraySegment<byte> value)
    {
        value.Validate();
        
        _localAllocHandle = Protect(value);
        _plaintextLength = (uint)value.Count;
    }
    
    
    public Secret(byte[] value) : this(new ArraySegment<byte>(value))
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
    }
        
    public Secret(byte* secret, int secretLength)
    {
        if (secret == null)
        {
            throw new ArgumentNullException(nameof(secret));
        }
        
        if (secretLength < 0)
        {
            throw Error.Common_ValueMustBeNonNegative(nameof(secretLength));
        }
        
        _localAllocHandle = Protect(secret, (uint)secretLength);
        _plaintextLength = (uint)secretLength;
    }
       
    public Secret(ISecret secret)
    {
        if (secret == null)
        {
            throw new ArgumentNullException(nameof(secret));
        }
        
        var other = secret as Secret;
        if (other != null)
        {
            // Fast-track: simple deep copy scenario.
            this._localAllocHandle = other._localAllocHandle.Duplicate();
            this._plaintextLength = other._plaintextLength;
        }
        else
        {
            // Copy the secret to a temporary managed buffer, then protect the buffer.
            // We pin the temp buffer and zero it out when we're finished to limit exposure of the secret.
            var tempPlaintextBuffer = new byte[secret.Length];
            fixed (byte* pbTempPlaintextBuffer = tempPlaintextBuffer)
            {
                try
                {
                    secret.WriteSecretIntoBuffer(new ArraySegment<byte>(tempPlaintextBuffer));
                    _localAllocHandle = Protect(pbTempPlaintextBuffer, (uint)tempPlaintextBuffer.Length);
                    _plaintextLength = (uint)tempPlaintextBuffer.Length;
                }
                finally
                {
                    UnsafeBufferUtil.SecureZeroMemory(pbTempPlaintextBuffer, tempPlaintextBuffer.Length);
                }
            }
        }
    }
    
    /* 创建 secret randomly */
    public static Secret Random(int numBytes)
    {
        if (numBytes < 0)
        {
            throw Error.Common_ValueMustBeNonNegative(nameof(numBytes));
        }
        
        if (numBytes == 0)
        {
            byte dummy;
            return new Secret(&dummy, 0);
        }
        else
        {
            // Don't use CNG if we're not on Windows.
            if (!OSVersionUtil.IsWindows())
            {
                return new Secret(ManagedGenRandomImpl.Instance.GenRandom(numBytes));
            }
            
            var bytes = new byte[numBytes];
            fixed (byte* pbBytes = bytes)
            {
                try
                {
                    BCryptUtil.GenRandom(pbBytes, (uint)numBytes);
                    return new Secret(pbBytes, numBytes);
                }
                finally
                {
                    UnsafeBufferUtil.SecureZeroMemory(pbBytes, numBytes);
                }
            }
        }
    }                                      
   
    /* write secret to buffer */
    public void WriteSecretIntoBuffer(ArraySegment<byte> buffer)
    {
        // Parameter checking
        buffer.Validate();
        if (buffer.Count != Length)
        {
            throw Error.Common_BufferIncorrectlySized(nameof(buffer), actualSize: buffer.Count, expectedSize: Length);
        }
        
        // only unprotect if the secret is zero-length, as CLR doesn't like pinning zero-length buffers
        if (Length != 0)
        {
            fixed (byte* pbBufferArray = buffer.Array)
            {
                UnprotectInto(&pbBufferArray[buffer.Offset]);
            }
        }
    }
        
    public void WriteSecretIntoBuffer(byte* buffer, int bufferLength)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        if (bufferLength != Length)
        {
            throw Error.Common_BufferIncorrectlySized(nameof(bufferLength), actualSize: bufferLength, expectedSize: Length);
        }
        
        if (Length != 0)
        {
            UnprotectInto(buffer);
        }
    }      
        
    public void Dispose()
    {
        _localAllocHandle.Dispose();
    }    
}

```

##### - protect

```c#
public unsafe sealed class Secret : IDisposable, ISecret
{
     private static SecureLocalAllocHandle Protect(ArraySegment<byte> plaintext)
    {
        fixed (byte* pbPlaintextArray = plaintext.Array)
        {
            return Protect(&pbPlaintextArray[plaintext.Offset], (uint)plaintext.Count);
        }
    }
    
    private static SecureLocalAllocHandle Protect(byte* pbPlaintext, uint cbPlaintext)
    {
        // If we're not running on a platform that supports CryptProtectMemory, shove the plaintext directly 
        // into a LocalAlloc handle. 
        // Ideally we'd mark this memory page as non-pageable, but this is fraught with peril.
        if (!OSVersionUtil.IsWindows())
        {
            var handle = SecureLocalAllocHandle.Allocate((IntPtr)checked((int)cbPlaintext));
            UnsafeBufferUtil.BlockCopy(
                from: pbPlaintext, 
                to: handle, 
                byteCount: cbPlaintext);
            
            return handle;
        }
        
        // We need to make sure we're a multiple of CRYPTPROTECTMEMORY_BLOCK_SIZE.
        var numTotalBytesToAllocate = cbPlaintext;
        var numBytesPaddingRequired = 
            CRYPTPROTECTMEMORY_BLOCK_SIZE - (numTotalBytesToAllocate % CRYPTPROTECTMEMORY_BLOCK_SIZE);
        if (numBytesPaddingRequired == CRYPTPROTECTMEMORY_BLOCK_SIZE)
        {
            // we're already a proper multiple of the block size
            numBytesPaddingRequired = 0; 
        }
        checked { numTotalBytesToAllocate += numBytesPaddingRequired; }
        CryptoUtil.Assert(
            numTotalBytesToAllocate % CRYPTPROTECTMEMORY_BLOCK_SIZE == 0, 
            "numTotalBytesToAllocate % CRYPTPROTECTMEMORY_BLOCK_SIZE == 0");
        
        // Allocate and copy plaintext data; padding is uninitialized / undefined.
        var encryptedMemoryHandle = SecureLocalAllocHandle.Allocate((IntPtr)numTotalBytesToAllocate);
        UnsafeBufferUtil.BlockCopy(
            from: pbPlaintext, 
            to: encryptedMemoryHandle, 
            byteCount: cbPlaintext);
        
        // Finally, CryptProtectMemory the whole mess.
        if (numTotalBytesToAllocate != 0)
        {
            MemoryProtection.CryptProtectMemory(
                encryptedMemoryHandle, 
                byteCount: numTotalBytesToAllocate);
        }
        return encryptedMemoryHandle;
    }
}

```

##### - unprotect

```c#
public unsafe sealed class Secret : IDisposable, ISecret
{
    private void UnprotectInto(byte* pbBuffer)
    {
        // If we're not running on a platform that supports CryptProtectMemory, the handle contains plaintext bytes.
        if (!OSVersionUtil.IsWindows())
        {
            UnsafeBufferUtil.BlockCopy(
                from: _localAllocHandle, 
                to: pbBuffer, 
                byteCount: _plaintextLength);
            
            return;
        }
        
        if (_plaintextLength % CRYPTPROTECTMEMORY_BLOCK_SIZE == 0)
        {
            // Case 1: Secret length is an exact multiple of the block size. Copy directly to the buffer and decrypt there.
            UnsafeBufferUtil.BlockCopy(
                from: _localAllocHandle, 
                to: pbBuffer, 
                byteCount: _plaintextLength);
            
            MemoryProtection.CryptUnprotectMemory(pbBuffer, _plaintextLength);
        }
        else
        {
            // Case 2: Secret length is not a multiple of the block size. We'll need to duplicate the data and
            // perform the decryption in the duplicate buffer, then copy the plaintext data over.
            using (var duplicateHandle = _localAllocHandle.Duplicate())
            {
                MemoryProtection.CryptUnprotectMemory(duplicateHandle, checked((uint)duplicateHandle.Length));
                
                UnsafeBufferUtil.BlockCopy(
                    from: duplicateHandle, 
                    to: pbBuffer, 
                    byteCount: _plaintextLength);
            }
        }
    }
}

```

#### 3.2 key

```c#
// 接口
public interface IKey
{    
    DateTimeOffset ActivationDate { get; }    
    DateTimeOffset CreationDate { get; }    
    DateTimeOffset ExpirationDate { get; }        
    bool IsRevoked { get; }
        
    Guid KeyId { get; }        
    
    IAuthenticatedEncryptorDescriptor Descriptor { get; }        
    IAuthenticatedEncryptor? CreateEncryptor();
}

```

##### 3.2.1 core

###### 3.2.1.1 扩展方法

```c#
internal static class KeyExtensions
{
    public static bool IsExpired(this IKey key, DateTimeOffset now)
    {
        return (key.ExpirationDate <= now);
    }
}

```

###### 3.2.1.2 key base

```c#
internal abstract class KeyBase : IKey
{
    private readonly Lazy<IAuthenticatedEncryptorDescriptor> _lazyDescriptor;
    private readonly IEnumerable<IAuthenticatedEncryptorFactory> _encryptorFactories;    
    
    // 内部保存 encryptor 实例（缓存）
    private IAuthenticatedEncryptor? _encryptor;
        
    public DateTimeOffset ActivationDate { get; }    
    public DateTimeOffset CreationDate { get; }    
    public DateTimeOffset ExpirationDate { get; }    
    
    public bool IsRevoked { get; private set; }
    internal void SetRevoked()
    {
        IsRevoked = true;
    }
    
    public Guid KeyId { get; }
    
    public IAuthenticatedEncryptorDescriptor Descriptor
    {
        get
        {
            return _lazyDescriptor.Value;
        }
    }
        
    public KeyBase(
        Guid keyId,
        DateTimeOffset creationDate,
        DateTimeOffset activationDate,
        DateTimeOffset expirationDate,
        Lazy<IAuthenticatedEncryptorDescriptor> lazyDescriptor,
        IEnumerable<IAuthenticatedEncryptorFactory> encryptorFactories)
    {
        KeyId = keyId;
        CreationDate = creationDate;
        ActivationDate = activationDate;
        ExpirationDate = expirationDate;
        _lazyDescriptor = lazyDescriptor;
        _encryptorFactories = encryptorFactories;
    }
           
    public IAuthenticatedEncryptor? CreateEncryptor()
    {
        // 如果 encryptor 为 null，
        if (_encryptor == null)
        {
            // 遍历 lazy factory，创建 encryptor，一旦成功创建即停止遍历
            foreach (var factory in _encryptorFactories)
            {
                var encryptor = factory.CreateEncryptorInstance(this);
                if (encryptor != null)
                {
                    _encryptor = encryptor;
                    break;
                }
            }
        }
        
        return _encryptor;
    }        
}

```

##### 3.2.2 实现

###### 3.2.2.1 key

```c#
internal sealed class Key : KeyBase
{
    public Key(
        Guid keyId,
        DateTimeOffset creationDate,
        DateTimeOffset activationDate,
        DateTimeOffset expirationDate,
        IAuthenticatedEncryptorDescriptor descriptor,
        IEnumerable<IAuthenticatedEncryptorFactory> encryptorFactories) : 
    		base(keyId,
                 creationDate,
                 activationDate,
                 expirationDate,
                 new Lazy<IAuthenticatedEncryptorDescriptor>(() => descriptor),
                 encryptorFactories)
    {
    }
}

```

###### 3.2.2.2 deferred key

```c#
internal sealed class DeferredKey : KeyBase
{
    public DeferredKey(
        Guid keyId,
        DateTimeOffset creationDate,
        DateTimeOffset activationDate,
        DateTimeOffset expirationDate,
        IInternalXmlKeyManager keyManager,
        XElement keyElement,
        IEnumerable<IAuthenticatedEncryptorFactory> encryptorFactories) : 
    		base(
                keyId,
                creationDate,
                activationDate,
                expirationDate,
                new Lazy<IAuthenticatedEncryptorDescriptor>(
                    GetLazyDescriptorDelegate(keyManager, keyElement)),
                encryptorFactories)
    {
    }

    private static Func<IAuthenticatedEncryptorDescriptor> GetLazyDescriptorDelegate(
        IInternalXmlKeyManager keyManager, 
        XElement keyElement)
    {
        // The <key> element will be held around in memory for a potentially lengthy period of time. 
        // Since it might contain sensitive information, we should protect it.
        var encryptedKeyElement = keyElement.ToSecret();
        
        try
        {
            return () => keyManager.DeserializeDescriptorFromKeyElement(encryptedKeyElement.ToXElement());
        }
        finally
        {
            // It's important that the lambda above doesn't capture 'descriptorElement'. 
            // Clearing the reference here helps us detect if we've done this by causing a null ref at runtime.
            keyElement = null!;
        }
    }
}

```

#### 3.3 key escrow sink

```c#
// 接口
public interface IKeyEscrowSink
{    
    void Store(Guid keyId, XElement element);
}

```

##### 3.3.1 扩展方法

```c#
internal static class KeyEscrowServiceProviderExtensions
{    
    // 扩展方法- get key escrow sink from di services
    public static IKeyEscrowSink? GetKeyEscrowSink(this IServiceProvider services)
    {
        var escrowSinks = services?.GetService<IEnumerable<IKeyEscrowSink>>()?.ToList();
        return (escrowSinks != null && escrowSinks.Count > 0) ? new AggregateKeyEscrowSink(escrowSinks) : null;
    }
    
    // 实现- aggregate escrow sink
    private sealed class AggregateKeyEscrowSink : IKeyEscrowSink
    {
        private readonly List<IKeyEscrowSink> _sinks;
        
        public AggregateKeyEscrowSink(List<IKeyEscrowSink> sinks)
        {
            _sinks = sinks;
        }
        
        public void Store(Guid keyId, XElement element)
        {
            foreach (var sink in _sinks)
            {
                sink.Store(keyId, element);
            }
        }
    }
}

```

#### 3.4 key store

##### 3.4.1 default key storage directory

```c#
// 接口
internal interface IDefaultKeyStorageDirectories
{
    DirectoryInfo? GetKeyStorageDirectory();    
    DirectoryInfo? GetKeyStorageDirectoryForAzureWebSites();
}

// 实现
internal sealed class DefaultKeyStorageDirectories : IDefaultKeyStorageDirectories
{
    public static IDefaultKeyStorageDirectories Instance { get; } = new DefaultKeyStorageDirectories();
            
    private DefaultKeyStorageDirectories()
    {
    }
    
    // (get) default directory local
    private static readonly Lazy<DirectoryInfo?> _defaultDirectoryLazy = 
        new Lazy<DirectoryInfo?>(GetKeyStorageDirectoryImpl);
    
    // On Windows, this currently corresponds to "Environment.SpecialFolder.LocalApplication/ASP.NET/DataProtection-Keys".
    // On Linux and macOS, this currently corresponds to "$HOME/.aspnet/DataProtection-Keys".    
    // This property can return null if no suitable default key storage directory can be found, such as the case when the 
    // user profile is unavailable.    
    public DirectoryInfo? GetKeyStorageDirectory() => _defaultDirectoryLazy.Value;
                    
    private static DirectoryInfo? GetKeyStorageDirectoryImpl()
    {
        DirectoryInfo retVal;
        
        // Environment.GetFolderPath returns null if the user profile isn't loaded.
        var localAppDataFromSystemPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var localAppDataFromEnvPath = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        var userProfilePath = Environment.GetEnvironmentVariable("USERPROFILE");
        var homePath = Environment.GetEnvironmentVariable("HOME");
        
        // 如果是 windows 系统，
        // 并且 local AppData 文件夹（地址 string）不为空
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && 
            !string.IsNullOrEmpty(localAppDataFromSystemPath))
        {
            // To preserve backwards-compatibility with 1.x, Environment.SpecialFolder.LocalApplicationData
            // cannot take precedence over $LOCALAPPDATA and $HOME/.aspnet on non-Windows platforms
            retVal = GetKeyStorageDirectoryFromBaseAppDataPath(
                localAppDataFromSystemPath);
        }
        // 如果 local AppDate 环境变量不为空，
        else if (localAppDataFromEnvPath != null)
        {
            retVal = GetKeyStorageDirectoryFromBaseAppDataPath(
                localAppDataFromEnvPath);
        }
        // 如果 user profile 环境变量不为空，
        else if (userProfilePath != null)
        {
            retVal = GetKeyStorageDirectoryFromBaseAppDataPath(
                Path.Combine(userProfilePath, "AppData", "Local"));
        }
        // 如果 home path 不为 null
        else if (homePath != null)
        {
            // If LOCALAPPDATA and USERPROFILE are not present but HOME is,
            // it's a good guess that this is a *NIX machine.  Use *NIX conventions for a folder name.
            retVal = new DirectoryInfo(Path.Combine(homePath, ".aspnet", DataProtectionKeysFolderName));
        }
        else if (!string.IsNullOrEmpty(localAppDataFromSystemPath))
        {
            // Starting in 2.x, non-Windows platforms may use Environment.SpecialFolder.LocalApplicationData
            // but only after checking for $LOCALAPPDATA, $USERPROFILE, and $HOME.
            retVal = GetKeyStorageDirectoryFromBaseAppDataPath(localAppDataFromSystemPath);
        }
        else
        {
            return null;
        }
        
        Debug.Assert(retVal != null);
        
        try
        {
            retVal.Create(); // throws if we don't have access, e.g., user profile not loaded
            return retVal;
        }
        catch
        {
            return null;
        }
    }
    
    private const string DataProtectionKeysFolderName = "DataProtection-Keys";
    
    private static DirectoryInfo GetKeyStorageDirectoryFromBaseAppDataPath(string basePath)
    {
        return new DirectoryInfo(Path.Combine(basePath, "ASP.NET", DataProtectionKeysFolderName));
    }
    
    // get directory for azure
    public DirectoryInfo? GetKeyStorageDirectoryForAzureWebSites()
    {
        // Azure Web Sites needs to be treated specially, as we need to store the keys in a correct persisted location. 
        // We use the existence of the %WEBSITE_INSTANCE_ID% env variable to determine if we're running in this environment, 
        // and if so we then use the %HOME% variable to build up our base key storage path.
        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")))
        {
            var homeEnvVar = Environment.GetEnvironmentVariable("HOME");
            if (!String.IsNullOrEmpty(homeEnvVar))
            {
                return GetKeyStorageDirectoryFromBaseAppDataPath(homeEnvVar);
            }
        }
        
        // nope
        return null;
    }        
}

```

##### 3.4.2 xml repository

```c#
public interface IXmlRepository
{        
    // get all top-level elements in the repository.    
    IReadOnlyCollection<XElement> GetAllElements();
    
    // The 'friendlyName' parameter must be unique if specified. 
    // For instance, it could be the id of the key being stored.    
    void StoreElement(XElement element, string friendlyName);
}

```

###### 3.4.2.1 ephemeral xml repo

```c#
internal class EphemeralXmlRepository : IXmlRepository
{
    // 内存容器
    private readonly List<XElement> _storedElements = new List<XElement>();
    
    public EphemeralXmlRepository(ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<EphemeralXmlRepository>();
        logger.UsingInmemoryRepository();
    }
    
    // 方法- get all elements
    public virtual IReadOnlyCollection<XElement> GetAllElements()
    {
        // force complete enumeration under lock for thread safety
        lock (_storedElements)
        {
            return GetAllElementsCore().ToList().AsReadOnly();
        }
    }
    
    private IEnumerable<XElement> GetAllElementsCore()
    {
        // this method must be called under lock
        foreach (XElement element in _storedElements)
        {
            // makes a deep copy so caller doesn't inadvertently modify it
            yield return new XElement(element); 
        }
    }
    
    // 方法- store element
    public virtual void StoreElement(XElement element, string friendlyName)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }
        
        // makes a deep copy so caller doesn't inadvertently modify it
        var cloned = new XElement(element); 
        
        // under lock for thread safety
        lock (_storedElements)
        {
            _storedElements.Add(cloned);
        }
    }
}

```

###### 3.4.2.2 file system xml repo

```c#
public class FileSystemXmlRepository : IXmlRepository
{
    private readonly ILogger _logger;
        
    // On Windows, this currently corresponds to "Environment.SpecialFolder.LocalApplication/ASP.NET/DataProtection-Keys".
    // On Linux and macOS, this currently corresponds to "$HOME/.aspnet/DataProtection-Keys".   
    public static DirectoryInfo? DefaultKeyStorageDirectory => DefaultKeyStorageDirectories.Instance.GetKeyStorageDirectory();
    public DirectoryInfo Directory { get; }
        
    public FileSystemXmlRepository(DirectoryInfo directory, ILoggerFactory loggerFactory)
    {
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));        
        _logger = loggerFactory.CreateLogger<FileSystemXmlRepository>();
        
        try
        {
            // 如果使用 docker 但是没有配置 volumn，-> warning
            if (ContainerUtils.IsContainer && !ContainerUtils.IsVolumeMountedFolder(Directory))
            {
                // warn users that keys may be lost when running in docker without a volume mounted folder
                _logger.UsingEphemeralFileSystemLocationInContainer(Directory.FullName);
            }
        }
        catch (Exception ex)
        {
            // Treat exceptions as non-fatal when attempting to detect docker.
            // These might occur if fstab is an unrecognized format, or if there are other unusual file IO errors.
            _logger.LogTrace(ex, "Failure occurred while attempting to detect docker.");
        }
    }
                      
    // 方法- get all elements
    public virtual IReadOnlyCollection<XElement> GetAllElements()
    {
        // forces complete enumeration
        return GetAllElementsCore().ToList().AsReadOnly();
    }
    
    private IEnumerable<XElement> GetAllElementsCore()
    {
        // won't throw if the directory already exists
        Directory.Create(); 
        
        // 遍历 directory 下所有 .xml 文件，读取 => xelement
        foreach (var fileSystemInfo in 
                 Directory.EnumerateFileSystemInfos("*.xml", SearchOption.TopDirectoryOnly))
        {
            yield return ReadElementFromFile(fileSystemInfo.FullName);
        }
    }
    
    private XElement ReadElementFromFile(string fullPath)
    {
        _logger.ReadingDataFromFile(fullPath);
        
        // read file by stream, and load to xelement
        using (var fileStream = File.OpenRead(fullPath))
        {
            return XElement.Load(fileStream);
        }
    }
                               
    // 方法- store element
    public virtual void StoreElement(XElement element, string friendlyName)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }
        
        if (!IsSafeFilename(friendlyName))
        {
            var newFriendlyName = Guid.NewGuid().ToString();
            _logger.NameIsNotSafeFileName(friendlyName, newFriendlyName);
            friendlyName = newFriendlyName;
        }
        
        StoreElementCore(element, friendlyName);
    }
    
    private static bool IsSafeFilename(string filename)
    {
        // Must be non-empty and contain only a-zA-Z0-9, hyphen, and underscore.
        return (!String.IsNullOrEmpty(filename) && 
                filename.All(c =>
                    c == '-' || 
                    c == '_' || 
                    ('0' <= c && c <= '9') || 
                    ('A' <= c && c <= 'Z') || 
                    ('a' <= c && c <= 'z')));
    }
    
    private void StoreElementCore(XElement element, string filename)
    {
        // We're first going to write the file to a temporary location. 
        // This way, another consumer won't try reading the file in the middle of us writing it. 
        // Additionally, if our process crashes mid-write, we won't end up with a corrupt .xml file.
        
        // won't throw if the directory already exists
        Directory.Create(); 
        
        var tempFilename = Path.Combine(Directory.FullName, Guid.NewGuid().ToString() + ".tmp");
        var finalFilename = Path.Combine(Directory.FullName, filename + ".xml");
        
        try
        {
            using (var tempFileStream = File.OpenWrite(tempFilename))
            {
                element.Save(tempFileStream);
            }
            
            // Once the file has been fully written, perform the rename.
            // Renames are atomic operations on the file systems we support.
            _logger.WritingDataToFile(finalFilename);
            
            try
            {
                // Prefer the atomic move operation to avoid multi-process startup issues
                File.Move(tempFilename, finalFilename);
            }
            catch (IOException)
            {
                // Use File.Copy because File.Move on NFS shares has issues in .NET Core 2.0
                // See https://github.com/dotnet/aspnetcore/issues/2941 for more context
                File.Copy(tempFilename, finalFilename);
            }
        }
        finally
        {
            // won't throw if the file doesn't exist
            File.Delete(tempFilename); 
        }
    }
}

```

###### 3.4.2.3 registry xml repo

```c#
[SupportedOSPlatform("windows")]
public class RegistryXmlRepository : IXmlRepository
{
    // logger
    private readonly ILogger _logger;
    
    // default registry key
    private static readonly Lazy<RegistryKey?> _defaultRegistryKeyLazy = 
        new Lazy<RegistryKey?>(GetDefaultHklmStorageKey);
    
    private static RegistryKey? GetDefaultHklmStorageKey()
    {
        try
        {
            var registryView = IntPtr.Size == 4 ? RegistryView.Registry32 : RegistryView.Registry64;
            // Try reading the auto-generated machine key from HKLM
            using (var hklmBaseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
            {
                // Even though this is in HKLM, WAS ensures that applications hosted in IIS are properly isolated.
                // See APP_POOL::EnsureSharedMachineKeyStorage in WAS source for more info.
                // The version number will need to change if IIS hosts Core CLR directly.
                var aspnetAutoGenKeysBaseKeyName = string.Format(
                    CultureInfo.InvariantCulture,
                    @"SOFTWARE\Microsoft\ASP.NET\4.0.30319.0\AutoGenKeys\{0}",
                    WindowsIdentity.GetCurrent()!.User!.Value);
                
                var aspnetBaseKey = hklmBaseKey.OpenSubKey(aspnetAutoGenKeysBaseKeyName, writable: true);
                if (aspnetBaseKey != null)
                {
                    using (aspnetBaseKey)
                    {
                        // We'll create a 'DataProtection' subkey under the auto-gen keys base
                        return aspnetBaseKey.OpenSubKey("DataProtection", writable: true)
                            ?? aspnetBaseKey.CreateSubKey("DataProtection");
                    }
                }
                return null; // couldn't find the auto-generated machine key
            }
        }
        catch
        {
            // swallow all errors; they're not fatal
            return null;
        }
    }    
    
    // The default key storage directory, 
    // which currently corresponds to "HKLM\SOFTWARE\Microsoft\ASP.NET\4.0.30319.0\AutoGenKeys\{SID}".              
    public static RegistryKey? DefaultRegistryKey => _defaultRegistryKeyLazy.Value;
    
    // registry key
    public RegistryKey RegistryKey { get; }
                
    public RegistryXmlRepository(RegistryKey registryKey, ILoggerFactory loggerFactory)
    {
        if (registryKey == null)
        {
            throw new ArgumentNullException(nameof(registryKey));
        }
        
        RegistryKey = registryKey;
        _logger = loggerFactory.CreateLogger<RegistryXmlRepository>();
    }
                   
    // 方法- get all elements   
    public virtual IReadOnlyCollection<XElement> GetAllElements()
    {
        // forces complete enumeration
        return GetAllElementsCore().ToList().AsReadOnly();
    }
    
    private IEnumerable<XElement> GetAllElementsCore()
    {
            
        foreach (string valueName in RegistryKey.GetValueNames())
        {
            var element = ReadElementFromRegKey(RegistryKey, valueName);
            if (element != null)
            {
                yield return element;
            }
        }
    }
    
    private XElement? ReadElementFromRegKey(RegistryKey regKey, string valueName)
    {
        _logger.ReadingDataFromRegistryKeyValue(regKey, valueName);
        
        var data = regKey.GetValue(valueName) as string;
        return (!string.IsNullOrEmpty(data)) ? XElement.Parse(data) : null;
    }
                           
    // 方法- store element
    public virtual void StoreElement(XElement element, string friendlyName)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }
        
        if (!IsSafeRegistryValueName(friendlyName))
        {
            var newFriendlyName = Guid.NewGuid().ToString();
            _logger.NameIsNotSafeRegistryValueName(friendlyName, newFriendlyName);
            friendlyName = newFriendlyName;
        }
        
        StoreElementCore(element, friendlyName);
    }
    
    private static bool IsSafeRegistryValueName(string filename)
    {
        // Must be non-empty and contain only a-zA-Z0-9, hyphen, and underscore.
        return (!String.IsNullOrEmpty(filename) && 
                filename.All(c =>
                    c == '-' || 
                    c == '_' || 
                    ('0' <= c && c <= '9') || 
                    ('A' <= c && c <= 'Z') || 
                    ('a' <= c && c <= 'z'))); 
    }
    
    private void StoreElementCore(XElement element, string valueName)
    {
        // Technically calls to RegSetValue* and RegGetValue* are atomic, so we don't have to worry about
        // another thread trying to read this value while we're writing it. There's still a small risk of
        // data corruption if power is lost while the registry file is being flushed to the file system,
        // but the window for that should be small enough that we shouldn't have to worry about it.
        RegistryKey.SetValue(valueName, element.ToString(), RegistryValueKind.String);
    }
}

```

##### 3.4.3 ef core repository

```c#
public class EntityFrameworkCoreXmlRepository<TContext> : IXmlRepository        
    where TContext : DbContext, IDataProtectionKeyContext
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
        
    public EntityFrameworkCoreXmlRepository(IServiceProvider services, ILoggerFactory loggerFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        _logger = loggerFactory.CreateLogger<EntityFrameworkCoreXmlRepository<TContext>>();
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }
    
    // 方法- get all elements
    public virtual IReadOnlyCollection<XElement> GetAllElements()
    {
        // forces complete enumeration
        return GetAllElementsCore().ToList().AsReadOnly();
        
        IEnumerable<XElement> GetAllElementsCore()
        {
            // 创建 scope，
            using (var scope = _services.CreateScope())
            {
                // 解析 TContext (db context)
                var context = scope.ServiceProvider.GetRequiredService<TContext>();
                // 遍历 context 的 data protection key
                foreach (var key in context.DataProtectionKeys.AsNoTracking())
                {
                    _logger.ReadingXmlFromKey(key.FriendlyName!, key.Xml);
                    
                    if (!string.IsNullOrEmpty(key.Xml))
                    {
                        // 转换成 xelement
                        yield return XElement.Parse(key.Xml);
                    }
                }
            }
        }
    }
    
    // 方法- store element
    public void StoreElement(XElement element, string friendlyName)
    {
        // 创建 scope，
        using (var scope = _services.CreateScope())
        {
            // 解析 TContext (db context)
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            // 创建 data protection key
            var newKey = new DataProtectionKey()
            {
                FriendlyName = friendlyName,
                Xml = element.ToString(SaveOptions.DisableFormatting)
            };
            // 注入 context 的 data protection keys
            context.DataProtectionKeys.Add(newKey);
            _logger.LogSavingKeyToDbContext(friendlyName, typeof(TContext).Name);
            context.SaveChanges();
        }
    }
}

```

###### 3.4.3.1 data protection key context

```c#
public interface IDataProtectionKeyContext
{    
    DbSet<DataProtectionKey> DataProtectionKeys { get; }
}

```

###### 3.4.3.2 data protection key

```c#
public class DataProtectionKey
{    
    public int Id { get; set; }        
    public string? FriendlyName { get; set; }        
    public string? Xml { get; set; }
}

```

###### 3.4.3.3 use ef core repository

```c#
public static IDataProtectionBuilder PersistKeysToDbContext<TContext>(this IDataProtectionBuilder builder)            
    where TContext : DbContext, IDataProtectionKeyContext
{
    builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
        services =>
        {
            var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            
            // 创建 ef core repo，注入 key management options
            return new ConfigureOptions<KeyManagementOptions>(
                options =>
                {
                    options.XmlRepository = new EntityFrameworkCoreXmlRepository<TContext>(services, loggerFactory);
                });
        });
    
    return builder;
}

```

##### 3.4.4 redis repository

```c#
public class RedisXmlRepository : IXmlRepository
{
    private readonly Func<IDatabase> _databaseFactory;
    private readonly RedisKey _key;
        
    public RedisXmlRepository(Func<IDatabase> databaseFactory, RedisKey key)
    {
        _databaseFactory = databaseFactory;
        _key = key;
    }
    
    // 方法- get all elements
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        return GetAllElementsCore().ToList().AsReadOnly();
    }
    
    private IEnumerable<XElement> GetAllElementsCore()
    {        
        var database = _databaseFactory();
        foreach (var value in database.ListRange(_key))
        {
            yield return XElement.Parse(value);
        }
    }
    
    // 方法- store element
    public void StoreElement(XElement element, string friendlyName)
    {
        var database = _databaseFactory();
        database.ListRightPush(_key, element.ToString(SaveOptions.DisableFormatting));
    }
}

```

###### 3.4.4.1 use redis repository

```c#
public static class StackExchangeRedisDataProtectionBuilderExtensions
{
    private const string DataProtectionKeysName = "DataProtection-Keys";
        
    public static IDataProtectionBuilder PersistKeysToStackExchangeRedis(
        this IDataProtectionBuilder builder, 
        IConnectionMultiplexer connectionMultiplexer)
    {
        return PersistKeysToStackExchangeRedis(
            builder, 
            connectionMultiplexer, 
            DataProtectionKeysName);
    }
    
    public static IDataProtectionBuilder PersistKeysToStackExchangeRedis(
        this IDataProtectionBuilder builder, 
        Func<IDatabase> databaseFactory, 
        RedisKey key)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (databaseFactory == null)
        {
            throw new ArgumentNullException(nameof(databaseFactory));
        }
    
        return PersistKeysToStackExchangeRedisInternal(
            builder, 
            databaseFactory, 
            key);
    }
     
    public static IDataProtectionBuilder PersistKeysToStackExchangeRedis(
        this IDataProtectionBuilder builder, 
        IConnectionMultiplexer connectionMultiplexer, 
        RedisKey key)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (connectionMultiplexer == null)
        {
            throw new ArgumentNullException(nameof(connectionMultiplexer));
        }
        
        return PersistKeysToStackExchangeRedisInternal(
            builder, 
            () => connectionMultiplexer.GetDatabase(), 
            key);
    }

    private static IDataProtectionBuilder PersistKeysToStackExchangeRedisInternal(
        IDataProtectionBuilder builder, 
        Func<IDatabase> databaseFactory, 
        RedisKey key)
    {
        // 创建 redis xml repository，注入 key management options
        builder.Services.Configure<KeyManagementOptions>(
            options =>
            {
                options.XmlRepository = new RedisXmlRepository(databaseFactory, key);
            });
        return builder;
    }
}

```

#### 3.5 activator

```c#
public interface IActivator
{    
    object CreateInstance(Type expectedBaseType, string implementationTypeName);
}

```

##### 3.5.1 扩展方法

```c#
internal static class ActivatorExtensions
{    
    // create T
    public static T CreateInstance<T>(this IActivator activator, string implementationTypeName)        
        where T : class
    {
        if (implementationTypeName == null)
        {
            throw new ArgumentNullException(nameof(implementationTypeName));
        }
        
        return activator.CreateInstance(typeof(T), implementationTypeName) as T
            ?? CryptoUtil.Fail<T>("CreateInstance returned null.");
    }
        
    // get activator from di services
    public static IActivator GetActivator(this IServiceProvider serviceProvider)
    {
        return (serviceProvider != null)
            ? (serviceProvider.GetService<IActivator>() ?? new SimpleActivator(serviceProvider))
            : SimpleActivator.DefaultWithoutServices;
    }
}

```

##### 3.5.2 simple activator

```c#
internal class SimpleActivator : IActivator
{
    private static readonly Type[] _serviceProviderTypeArray = { typeof(IServiceProvider) };
    
    internal static readonly SimpleActivator DefaultWithoutServices = new SimpleActivator(null);
    
    private readonly IServiceProvider? _services;
    
    public SimpleActivator(IServiceProvider? services)
    {
        _services = services;
    }
    
    public virtual object CreateInstance(Type expectedBaseType, string implementationTypeName)
    {
        // Would the assignment even work?
        var implementationType = Type.GetType(implementationTypeName, throwOnError: true)!;
        expectedBaseType.AssertIsAssignableFrom(implementationType);
        
        // If no IServiceProvider was specified, prefer .ctor() [if it exists]
        if (_services == null)
        {
            var ctorParameterless = implementationType.GetConstructor(Type.EmptyTypes);
            if (ctorParameterless != null)
            {
                return Activator.CreateInstance(implementationType)!;
            }
        }
        
        // If an IServiceProvider was specified or if .ctor() doesn't exist, prefer .ctor(IServiceProvider) [if it exists]
        var ctorWhichTakesServiceProvider = implementationType.GetConstructor(_serviceProviderTypeArray);
        if (ctorWhichTakesServiceProvider != null)
        {
            return ctorWhichTakesServiceProvider.Invoke(new[] { _services });
        }
        
        // Finally, prefer .ctor() as an ultimate fallback.
        // This will throw if the ctor cannot be called.
        return Activator.CreateInstance(implementationType)!;
    }
}

```

##### 3.5.3 type forwarding activator

```c#
internal class TypeForwardingActivator : SimpleActivator
{
    private const string OldNamespace = "Microsoft.AspNet.DataProtection";
    private const string CurrentNamespace = "Microsoft.AspNetCore.DataProtection";
    private readonly ILogger _logger;
    
    public TypeForwardingActivator(IServiceProvider services) :     	
    	this(services, NullLoggerFactory.Instance)
    {
    }
    
    public TypeForwardingActivator(IServiceProvider services, ILoggerFactory loggerFactory) : base(services)
    {
        _logger = loggerFactory.CreateLogger(typeof(TypeForwardingActivator));
    }
    
    public override object CreateInstance(Type expectedBaseType, string originalTypeName) => 
        CreateInstance(expectedBaseType, originalTypeName, out var _);
    
    // for testing
    internal object CreateInstance(
        Type expectedBaseType, 
        string originalTypeName, 
        out bool forwarded)
    {
        var forwardedTypeName = originalTypeName;
        var candidate = false;
        if (originalTypeName.Contains(OldNamespace))
        {
            candidate = true;
            forwardedTypeName = originalTypeName.Replace(OldNamespace, CurrentNamespace);
        }
        
        if (candidate || forwardedTypeName.StartsWith(CurrentNamespace + ".", StringComparison.Ordinal))
        {
            candidate = true;
            forwardedTypeName = RemoveVersionFromAssemblyName(forwardedTypeName);
        }
        
        if (candidate)
        {
            var type = Type.GetType(forwardedTypeName, false);
            if (type != null)
            {
                _logger.LogDebug(
                    "Forwarded activator type request from {FromType} to {ToType}",
                    originalTypeName,
                    forwardedTypeName);
                forwarded = true;
                return base.CreateInstance(expectedBaseType, forwardedTypeName);
            }
        }
        
        forwarded = false;
        return base.CreateInstance(expectedBaseType, originalTypeName);
    }
    
    protected string RemoveVersionFromAssemblyName(string forwardedTypeName)
    {
        // Type, Assembly, Version={Version}, Culture={Culture}, PublicKeyToken={Token}
        
        var versionStartIndex = forwardedTypeName.IndexOf(", Version=", StringComparison.Ordinal);
        while (versionStartIndex != -1)
        {
            var versionEndIndex = forwardedTypeName.IndexOf(',', versionStartIndex + ", Version=".Length);
            
            if (versionEndIndex == -1)
            {
                // No end index, so are done and can remove the rest
                return forwardedTypeName.Substring(0, versionStartIndex);
            }
            
            forwardedTypeName = forwardedTypeName.Remove(versionStartIndex, versionEndIndex - versionStartIndex);
            versionStartIndex = forwardedTypeName.IndexOf(", Version=", StringComparison.Ordinal);
        }
        
        // No version left
        return forwardedTypeName;        
    }
}

```

#### 3.6 xml encryptor & decryptor

##### 3.6.1 core

###### 3.6.1.1 xml encryptor

```c#
public interface IXmlEncryptor
{    
    // Implementations of this method must not mutate the "XElement" instance provided by "plaintextElement"    
    EncryptedXmlInfo Encrypt(XElement plaintextElement);
}

```

###### 3.6.1.2 encrypted xml info

```c#
public sealed class EncryptedXmlInfo
{
    public Type DecryptorType { get; }        
    public XElement EncryptedElement { get; }
    
    public EncryptedXmlInfo(XElement encryptedElement, Type decryptorType)
    {
        if (encryptedElement == null)
        {
            throw new ArgumentNullException(nameof(encryptedElement));
        }        
        if (decryptorType == null)
        {
            throw new ArgumentNullException(nameof(decryptorType));
        }
        
        if (!typeof(IXmlDecryptor).IsAssignableFrom(decryptorType))
        {
            throw new ArgumentException(
                Resources.FormatTypeExtensions_BadCast(decryptorType.FullName, typeof(IXmlDecryptor).FullName),
                nameof(decryptorType));
        }
        
        EncryptedElement = encryptedElement;
        DecryptorType = decryptorType;
    }            
}

```

###### 3.6.1.3 扩展方法

```c#
internal unsafe static class XmlEncryptionExtensions
{
    // encrypt xelement
    public static XElement? EncryptIfNecessary(this IXmlEncryptor encryptor, XElement element)
    {
        // 如果不需要 encryption，-> 返回 null
        if (!DoesElementOrDescendentRequireEncryption(element))
        {
            return null;
        }
        
        // Deep copy the element (since we're going to mutate) and put it into a document to guarantee it has a parent.
        var doc = new XDocument(new XElement(element));
        
        // We remove elements from the document as we encrypt them and perform
        // fix-up later. This keeps us from going into an infinite loop in
        // the case of a null encryptor (which returns its original input which
        // is still marked as 'requires encryption').
        var placeholderReplacements = new Dictionary<XElement, EncryptedXmlInfo>();
        
        while (true)
        {
            var elementWhichRequiresEncryption = 
                doc.Descendants().FirstOrDefault(DoesSingleElementRequireEncryption);
            if (elementWhichRequiresEncryption == null)
            {
                // All encryption is finished.
                break;
            }
            
            // Encrypt the clone so that the encryptor doesn't inadvertently modify the original document 
            // or other data structures.
            var clonedElementWhichRequiresEncryption = new XElement(elementWhichRequiresEncryption);
            var innerDoc = new XDocument(clonedElementWhichRequiresEncryption);
            var encryptedXmlInfo = encryptor.Encrypt(clonedElementWhichRequiresEncryption);
            CryptoUtil.Assert(encryptedXmlInfo != null, "IXmlEncryptor.Encrypt returned null.");
            
            // Put a placeholder into the original document so that we can continue our
            // search for elements which need to be encrypted.
            var newPlaceholder = new XElement("placeholder");
            placeholderReplacements[newPlaceholder] = encryptedXmlInfo;
            elementWhichRequiresEncryption.ReplaceWith(newPlaceholder);
        }
        
        // Finally, perform fixup.
        Debug.Assert(placeholderReplacements.Count > 0);
        foreach (var entry in placeholderReplacements)
        {
            // <enc:encryptedSecret decryptorType="{type}" xmlns:enc="{ns}">
            //   <element />
            // </enc:encryptedSecret>
            entry.Key.ReplaceWith(new XElement(
                XmlConstants.EncryptedSecretElementName,
                new XAttribute(
                    XmlConstants.DecryptorTypeAttributeName, 
                    entry.Value.DecryptorType.AssemblyQualifiedName!),
                entry.Value.EncryptedElement));
        }
        return doc.Root;
    }
    
    // decrypt xelement
    public static XElement DecryptElement(this XElement element, IActivator activator)
    {
        // If no decryption necessary, return original element.
        if (!DoesElementOrDescendentRequireDecryption(element))
        {
            return element;
        }
        
        // Deep copy the element (since we're going to mutate) and put it into a document to guarantee it has a parent.
        var doc = new XDocument(new XElement(element));
        
        // We remove elements from the document as we decrypt them and perform fix-up later. 
        // This keeps us from going into an infinite loop in the case of a null decryptor (which returns its original input which
        // is still marked as 'requires decryption').
        var placeholderReplacements = new Dictionary<XElement, XElement>();
        
        while (true)
        {
            var elementWhichRequiresDecryption = 
                doc.Descendants(XmlConstants.EncryptedSecretElementName).FirstOrDefault();
            if (elementWhichRequiresDecryption == null)
            {
                // All encryption is finished.
                break;
            }
            
            // Decrypt the clone so that the decryptor doesn't inadvertently modify the original document or other data structures. 
            // The element we pass to the decryptor should be the child of the 'encryptedSecret' element.
            var clonedElementWhichRequiresDecryption = new XElement(elementWhichRequiresDecryption);
            string decryptorTypeName = (string)clonedElementWhichRequiresDecryption.Attribute(XmlConstants.DecryptorTypeAttributeName)!;
            var decryptorInstance = activator.CreateInstance<IXmlDecryptor>(decryptorTypeName);
            var decryptedElement = decryptorInstance.Decrypt(clonedElementWhichRequiresDecryption.Elements().Single());
            
            // Put a placeholder into the original document so that we can continue our search for elements which need to be decrypted.
            var newPlaceholder = new XElement("placeholder");
            placeholderReplacements[newPlaceholder] = decryptedElement;
            elementWhichRequiresDecryption.ReplaceWith(newPlaceholder);
        }
        
        // Finally, perform fixup.
        Debug.Assert(placeholderReplacements.Count > 0);
        foreach (var entry in placeholderReplacements)
        {
            entry.Key.ReplaceWith(entry.Value);
        }
        return doc.Root!;
    }
    
    // to secret       
    public static Secret ToSecret(this XElement element)
    {
        // 16k buffer should be large enough to encrypt any realistic secret
        const int DEFAULT_BUFFER_SIZE = 16 * 1024; 
        var memoryStream = new MemoryStream(DEFAULT_BUFFER_SIZE);
        element.Save(memoryStream);
        
        var underlyingBuffer = memoryStream.GetBuffer();
        // try to limit this moving around in memory while we allocate
        fixed (byte* __unused__ = underlyingBuffer) 
        {
            try
            {
                return new Secret(new ArraySegment<byte>(underlyingBuffer, 0, checked((int)memoryStream.Length)));
            }
            finally
            {
                Array.Clear(underlyingBuffer, 0, underlyingBuffer.Length);
            }
        }
    }
    
    // to xelement
    public static XElement ToXElement(this Secret secret)
    {
        var plaintextSecret = new byte[secret.Length];
        
        // try to keep the GC from moving it around
        fixed (byte* __unused__ = plaintextSecret) 
        {
            try
            {
                secret.WriteSecretIntoBuffer(new ArraySegment<byte>(plaintextSecret));
                var memoryStream = new MemoryStream(plaintextSecret, writable: false);
                return XElement.Load(memoryStream);
            }
            finally
            {
                Array.Clear(plaintextSecret, 0, plaintextSecret.Length);
            }
        }
    }
    
    // require encryption for single element
    private static bool DoesSingleElementRequireEncryption(XElement element)
    {
        return element.IsMarkedAsRequiringEncryption();
    }
    // require encryption for element or descendent    
    private static bool DoesElementOrDescendentRequireEncryption(XElement element)
    {
        return element.DescendantsAndSelf().Any(DoesSingleElementRequireEncryption);
    }
    // require decryption for element or descendent    
    private static bool DoesElementOrDescendentRequireDecryption(XElement element)
    {
        return element.DescendantsAndSelf(XmlConstants.EncryptedSecretElementName).Any();
    }                
}

```

###### 3.6.1.3 xml decrptyor

```c#
public interface IXmlDecryptor
{    
    // Implementations of this method must not mutate the "XElement" instance provided by "encryptedElement"   
    XElement Decrypt(XElement encryptedElement);
}

```

##### 3.6.2 null impl

###### 3.6.2.1 null xml encryptor

```c#
public sealed class NullXmlEncryptor : IXmlEncryptor
{
    private readonly ILogger _logger;
        
    public NullXmlEncryptor() : this(services: null)
    {
    }
        
    public NullXmlEncryptor(IServiceProvider? services)
    {
        _logger = services.GetLogger<NullXmlEncryptor>();
    }
        
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        if (plaintextElement == null)
        {
            throw new ArgumentNullException(nameof(plaintextElement));
        }
        
        _logger.EncryptingUsingNullEncryptor();
        
        // <unencryptedKey>
        //   <!-- This key is not encrypted. -->
        //   <plaintextElement />
        // </unencryptedKey>
        
        var newElement = new XElement(
            "unencryptedKey",
            new XComment(" This key is not encrypted. "),
            new XElement(plaintextElement));
        
        return new EncryptedXmlInfo(newElement, typeof(NullXmlDecryptor));
    }
}

```

###### 3.6.2.2 null xml decryptor

```c#
public sealed class NullXmlDecryptor : IXmlDecryptor
{    
    public XElement Decrypt(XElement encryptedElement)
    {
        if (encryptedElement == null)
        {
            throw new ArgumentNullException(nameof(encryptedElement));
        }
        
        // <unencryptedKey>
        //   <!-- This key is not encrypted. -->
        //   <plaintextElement />
        // </unencryptedKey>
        
        // Return a clone of the single child node.
        return new XElement(encryptedElement.Elements().Single());
    }
}

```

##### 3.6.3 dpapi impl

###### 3.6.3.1 dpapi xml encryptor

```c#
[SupportedOSPlatform("windows")]
public sealed class DpapiXmlEncryptor : IXmlEncryptor
{
    private readonly ILogger _logger;
    private readonly bool _protectToLocalMachine;
    
    
    public DpapiXmlEncryptor(bool protectToLocalMachine, ILoggerFactory loggerFactory)
    {
        // 验证 windows 系统
        CryptoUtil.AssertPlatformIsWindows();
        
        _protectToLocalMachine = protectToLocalMachine;
        _logger = loggerFactory.CreateLogger<DpapiXmlEncryptor>();
    }
    
    // 方法- encrypt
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        if (plaintextElement == null)
        {
            throw new ArgumentNullException(nameof(plaintextElement));
        }
        if (_protectToLocalMachine)
        {
            _logger.EncryptingToWindowsDPAPIForLocalMachineAccount();
        }
        else
        {
            _logger.EncryptingToWindowsDPAPIForCurrentUserAccount(WindowsIdentity.GetCurrent().Name);
        }
        
        // Convert the XML element to a binary secret so that it can be run through DPAPI
        byte[] dpapiEncryptedData;
        try
        {
            using (var plaintextElementAsSecret = plaintextElement.ToSecret())
            {
                dpapiEncryptedData = DpapiSecretSerializerHelper.ProtectWithDpapi(
                    plaintextElementAsSecret, 
                    protectToLocalMachine: _protectToLocalMachine);
            }
        }
        catch (Exception ex)
        {
            _logger.ErrorOccurredWhileEncryptingToWindowsDPAPI(ex);
            throw;
        }
        
        // <encryptedKey>
        //   <!-- This key is encrypted with {provider}. -->
        //   <value>{base64}</value>
        // </encryptedKey>
        
        var element = new XElement(
            "encryptedKey",
            new XComment(" This key is encrypted with Windows DPAPI. "),
            new XElement("value", Convert.ToBase64String(dpapiEncryptedData)));
        
        return new EncryptedXmlInfo(element, typeof(DpapiXmlDecryptor));
    }
}

```

###### 3.6.3.2 dapai xml decryptor

```c#
public sealed class DpapiXmlDecryptor : IXmlDecryptor
{
    private readonly ILogger _logger;
        
    public DpapiXmlDecryptor()  this(services: null)
    {
    }
        
    public DpapiXmlDecryptor(IServiceProvider? services)
    {
        // 验证 windows 系统
        CryptoUtil.AssertPlatformIsWindows();
        
        _logger = services.GetLogger<DpapiXmlDecryptor>();
    }
    
    // 方法- decrypt
    public XElement Decrypt(XElement encryptedElement)
    {
        if (encryptedElement == null)
        {
            throw new ArgumentNullException(nameof(encryptedElement));
        }
        
        _logger.DecryptingSecretElementUsingWindowsDPAPI();
        
        try
        {
            // <encryptedKey>
            //   <!-- This key is encrypted with {provider}. -->
            //   <value>{base64}</value>
            // </encryptedKey>
            
            var protectedSecret = Convert.FromBase64String((string)encryptedElement.Element("value")!);
            using (var secret = DpapiSecretSerializerHelper.UnprotectWithDpapi(protectedSecret))
            {
                return secret.ToXElement();
            }
        }
        catch (Exception ex)
        {            
            _logger.ExceptionOccurredTryingToDecryptElement(ex);
            throw;
        }
    }
}

```

##### 3.6.4 dpapi ng impl

###### 3.6.4.1 dpapi ng xml encryptor

```c#
[SupportedOSPlatform("windows")]
public sealed class DpapiNGXmlEncryptor : IXmlEncryptor
{
    private readonly ILogger _logger;
    private readonly NCryptDescriptorHandle _protectionDescriptorHandle;
            
    public DpapiNGXmlEncryptor(
        string protectionDescriptorRule, 
        DpapiNGProtectionDescriptorFlags flags, 
        ILoggerFactory loggerFactory)
    {
        if (protectionDescriptorRule == null)
        {
            throw new ArgumentNullException(nameof(protectionDescriptorRule));
        }
        
        // 验证 windows 8 系统
        CryptoUtil.AssertPlatformIsWindows8OrLater();
        
        var ntstatus = UnsafeNativeMethods.NCryptCreateProtectionDescriptor(
            protectionDescriptorRule, 
            (uint)flags, 
            out _protectionDescriptorHandle);
        
        UnsafeNativeMethods.ThrowExceptionForNCryptStatus(ntstatus);
        CryptoUtil.AssertSafeHandleIsValid(_protectionDescriptorHandle);
        
        _logger = loggerFactory.CreateLogger<DpapiNGXmlEncryptor>();
    }
    
        
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        if (plaintextElement == null)
        {
            throw new ArgumentNullException(nameof(plaintextElement));
        }
        
        var protectionDescriptorRuleString = _protectionDescriptorHandle.GetProtectionDescriptorRuleString();
        _logger.EncryptingToWindowsDPAPINGUsingProtectionDescriptorRule(protectionDescriptorRuleString);
        
        // Convert the XML element to a binary secret so that it can be run through DPAPI
        byte[] cngDpapiEncryptedData;
        try
        {
            using (var plaintextElementAsSecret = plaintextElement.ToSecret())
            {
                cngDpapiEncryptedData = DpapiSecretSerializerHelper.ProtectWithDpapiNG(
                    plaintextElementAsSecret, 
                    _protectionDescriptorHandle);
            }
        }
        catch (Exception ex)
        {
            _logger.ErrorOccurredWhileEncryptingToWindowsDPAPING(ex);
            throw;
        }
        
        // <encryptedKey>
        //   <!-- This key is encrypted with {provider}. -->
        //   <!-- rule string -->
        //   <value>{base64}</value>
        // </encryptedKey>
        
        var element = new XElement(
            "encryptedKey",
            new XComment(" This key is encrypted with Windows DPAPI-NG. "),
            new XComment(" Rule: " + protectionDescriptorRuleString + " "),
            new XElement("value", Convert.ToBase64String(cngDpapiEncryptedData)));
        
        return new EncryptedXmlInfo(element, typeof(DpapiNGXmlDecryptor));
    }
       
    internal static string GetDefaultProtectionDescriptorString()
    {
        CryptoUtil.AssertPlatformIsWindows8OrLater();
        
        // Creates a SID=... protection descriptor string for the current user.
        // Reminder: DPAPI:NG provides only encryption, not authentication.
        using (var currentIdentity = WindowsIdentity.GetCurrent())
        {
            // use the SID to create an SDDL string
            return string.Format(CultureInfo.InvariantCulture, "SID={0}", currentIdentity?.User?.Value);
        }
    }
}

```

###### 3.6.4.2 dpapi ng protection descriptor flag

```c#
[Flags]
public enum DpapiNGProtectionDescriptorFlags
{    
    // No special handling is necessary.    
    None = 0,
    
    // The provided descriptor is a reference to a full descriptor stored in the system registry.    
    NamedDescriptor = 0x00000001,
        
    // When combined with "NamedDescriptor", uses the HKLM registry instead of the HKCU registry when locating the full descriptor.    
    MachineKey = 0x00000020,
}

```

###### 3.6.4.3 dpapi ng xml decryptor

```c#
public sealed class DpapiNGXmlDecryptor : IXmlDecryptor
{
    private readonly ILogger _logger;
        
    public DpapiNGXmlDecryptor() : this(services: null)
    {
    }
        
    public DpapiNGXmlDecryptor(IServiceProvider? services)
    {
        CryptoUtil.AssertPlatformIsWindows8OrLater();        
        _logger = services.GetLogger<DpapiNGXmlDecryptor>();
    }
    
    // 方法- decrypt
    public XElement Decrypt(XElement encryptedElement)
    {
        if (encryptedElement == null)
        {
            throw new ArgumentNullException(nameof(encryptedElement));
        }
        
        try
        {
            // <encryptedKey>
            //   <!-- This key is encrypted with {provider}. -->
            //   <!-- rule string -->
            //   <value>{base64}</value>
            // </encryptedKey>
            
            var protectedSecret = Convert.FromBase64String((string)encryptedElement.Element("value")!);
            if (_logger.IsDebugLevelEnabled())
            {
                string? protectionDescriptorRule;
                try
                {
                    protectionDescriptorRule = DpapiSecretSerializerHelper.GetRuleFromDpapiNGProtectedPayload(protectedSecret);
                }
                catch
                {
                    // swallow all errors - it's just a log
                    protectionDescriptorRule = null;
                }
                _logger.DecryptingSecretElementUsingWindowsDPAPING(protectionDescriptorRule);
            }
            
            using (var secret = DpapiSecretSerializerHelper.UnprotectWithDpapiNG(protectedSecret))
            {
                return secret.ToXElement();
            }
        }
        catch (Exception ex)
        {            
            _logger.ExceptionOccurredTryingToDecryptElement(ex);
            throw;
        }
    }
}

```

##### 3.6.5 cert encrypted impl

###### 3.6.5.0 encrypted xml?



###### 3.6.5.1 certificate resolver

```c#
// 接口
public interface ICertificateResolver
{        
    X509Certificate2? ResolveCertificate(string thumbprint);
}

// 实现
public class CertificateResolver : ICertificateResolver
{
   
    public virtual X509Certificate2? ResolveCertificate(string thumbprint)
    {
        if (thumbprint == null)
        {
            throw new ArgumentNullException(nameof(thumbprint));
        }        
        if (String.IsNullOrEmpty(thumbprint))
        {
            throw Error.Common_ArgumentCannotBeNullOrEmpty(nameof(thumbprint));
        }
        
        return GetCertificateFromStore(StoreLocation.CurrentUser, thumbprint)
            ?? GetCertificateFromStore(StoreLocation.LocalMachine, thumbprint);
    }
    
    private static X509Certificate2? GetCertificateFromStore(StoreLocation location, string thumbprint)
    {
        // 创建 x509 store
        var store = new X509Store(location);
        
        try
        {
            // open store
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            // find matching certs
            var matchingCerts = store.Certificates.Find(
                X509FindType.FindByThumbprint, 
                thumbprint, 
                validOnly: true);
            // 返回 first matching cert
            return (matchingCerts != null && matchingCerts.Count > 0)
                ? matchingCerts[0]
                : null;
        }
        catch (CryptographicException)
        {
            // Suppress first-chance exceptions when opening the store.
            // For example, LocalMachine\My is not supported on Linux yet and will throw on Open(),
            // but there isn't a good way to detect this without attempting to open the store.
            // See https://github.com/dotnet/corefx/issues/3690.
            return null;
        }
        finally
        {
            store.Close();
        }
    }
}

```

###### 3.6.5.2 certificated xml encryptor

```c#
// 扩展接口
internal interface IInternalCertificateXmlEncryptor
{
    EncryptedData PerformEncryption(EncryptedXml encryptedXml, XmlElement elementToEncrypt);
}

// certificate xml encryptor
public sealed class CertificateXmlEncryptor : 
	IInternalCertificateXmlEncryptor, 
	IXmlEncryptor
{
    private readonly Func<X509Certificate2> _certFactory;
    private readonly IInternalCertificateXmlEncryptor _encryptor;
    private readonly ILogger _logger;
       
    public CertificateXmlEncryptor(
        string thumbprint, 
        ICertificateResolver certificateResolver, 
        ILoggerFactory loggerFactory) : 
    		this(loggerFactory, encryptor: null)
    {
        if (thumbprint == null)
        {
            throw new ArgumentNullException(nameof(thumbprint));
        }        
        if (certificateResolver == null)
        {
            throw new ArgumentNullException(nameof(certificateResolver));
        }
        
        // 1- 创建 cert factory
        _certFactory = CreateCertFactory(thumbprint, certificateResolver);
    }
    // 1- 
    private Func<X509Certificate2> CreateCertFactory(
        string thumbprint, 
        ICertificateResolver resolver)
    {
        return () =>
        {
            try
            {
                var cert = resolver.ResolveCertificate(thumbprint);
                if (cert == null)
                {
                    throw Error.CertificateXmlEncryptor_CertificateNotFound(thumbprint);
                }
                return cert;
            }
            catch (Exception ex)
            {
                _logger.ExceptionWhileTryingToResolveCertificateWithThumbprint(thumbprint, ex);                
                throw;
            }
        };
    }    
           
    public CertificateXmlEncryptor(
        X509Certificate2 certificate, 
        ILoggerFactory loggerFactory) : 
    		this(loggerFactory, encryptor: null)
    {
        if (certificate == null)
        {
            throw new ArgumentNullException(nameof(certificate));
        }
        
        _certFactory = () => certificate;
    }
    
    internal CertificateXmlEncryptor(
        ILoggerFactory loggerFactory, 
        IInternalCertificateXmlEncryptor? encryptor)
    {
        // 注入 cert xml encryptor（没有则注入自己）
        _encryptor = encryptor ?? this;
        _logger = loggerFactory.CreateLogger<CertificateXmlEncryptor>();
        // Set by calling ctors
        _certFactory = default!; 
    }

    // 方法- encrypt   
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        if (plaintextElement == null)
        {
            throw new ArgumentNullException(nameof(plaintextElement));
        }
        
        // <EncryptedData Type="http://www.w3.org/2001/04/xmlenc#Element" xmlns="http://www.w3.org/2001/04/xmlenc#">
        //   ...
        // </EncryptedData>
        
        // 2-
        var encryptedElement = EncryptElement(plaintextElement);
        return new EncryptedXmlInfo(encryptedElement, typeof(EncryptedXmlDecryptor));
    }
    // 2-
    private XElement EncryptElement(XElement plaintextElement)
    {
        // EncryptedXml works with XmlDocument, not XLinq. 
        // When we perform the conversion we'll wrap the incoming element in a dummy <root /> element since encrypted XML 
        // doesn't handle encrypting the root element all that well.
        var xmlDocument = new XmlDocument();
        xmlDocument.Load(new XElement("root", plaintextElement).CreateReader());
        var elementToEncrypt = (XmlElement)xmlDocument.DocumentElement!.FirstChild!;
        
        // Perform the encryption and update the document in-place.
        var encryptedXml = new EncryptedXml(xmlDocument);
        // 3- 使用 internal xml cert encryptor 的 perform encryption 方法
        var encryptedData = _encryptor.PerformEncryption(encryptedXml, elementToEncrypt);
        EncryptedXml.ReplaceElement(elementToEncrypt, encryptedData, content: false);
        
        // Strip the <root /> element back off and convert the XmlDocument to an XElement.
        return XElement.Load(xmlDocument.DocumentElement.FirstChild!.CreateNavigator()!.ReadSubtree());
    }
        
    // 3- 接口方法- perform encryption
    EncryptedData IInternalCertificateXmlEncryptor.PerformEncryption(
        EncryptedXml encryptedXml, 
        XmlElement elementToEncrypt)
    {
        // 创建 cert
        var cert = _certFactory() ?? CryptoUtil.Fail<X509Certificate2>("Cert factory returned null.");        
        _logger.EncryptingToX509CertificateWithThumbprint(cert.Thumbprint);
        
        try
        {
            // 执行 encrypted xml 的 encrypt 方法
            return encryptedXml.Encrypt(elementToEncrypt, cert);
        }
        catch (Exception ex)
        {
            _logger.AnErrorOccurredWhileEncryptingToX509CertificateWithThumbprint(cert.Thumbprint, ex);
            throw;
        }
    }
}

```

###### 3.6.5.3 encrypted xml decryptor

```c#
// 接口
internal interface IInternalEncryptedXmlDecryptor
{
    void PerformPreDecryptionSetup(EncryptedXml encryptedXml);
}

// 实现
public sealed class EncryptedXmlDecryptor : 
	IInternalEncryptedXmlDecryptor, 
	IXmlDecryptor
{
    private readonly IInternalEncryptedXmlDecryptor _decryptor;
    private readonly XmlKeyDecryptionOptions? _options;
        
    public EncryptedXmlDecryptor() : this(services: null)
    {
    }
      
    public EncryptedXmlDecryptor(IServiceProvider? services)
    {
        // 从 di 解析 internal encrypted xml decryptor（没有则注入自己）
        _decryptor = services?.GetService<IInternalEncryptedXmlDecryptor>() ?? this;
        // 从 di 解析 xml key decryption options
        _options = services?.GetService<IOptions<XmlKeyDecryptionOptions>>()?.Value;
    }
       
    public XElement Decrypt(XElement encryptedElement)
    {
        if (encryptedElement == null)
        {
            throw new ArgumentNullException(nameof(encryptedElement));
        }
        
        // <EncryptedData Type="http://www.w3.org/2001/04/xmlenc#Element" xmlns="http://www.w3.org/2001/04/xmlenc#">
        //   ...
        // </EncryptedData>
        
        // EncryptedXml works with XmlDocument, not XLinq. 
        // When we perform the conversion we'll wrap the incoming element in a dummy <root /> element since encrypted XML 
        // doesn't handle encrypting the root element all that well.
        var xmlDocument = new XmlDocument();
        xmlDocument.Load(new XElement("root", encryptedElement).CreateReader());
        
        // Perform the decryption and update the document in-place.
        var encryptedXml = new EncryptedXmlWithCertificateKeys(_options, xmlDocument);
        // 1- 执行 internal xml decryptor 的 perform pre decryption setup 方法
        _decryptor.PerformPreDecryptionSetup(encryptedXml);        
        encryptedXml.DecryptDocument();
        
        // Strip the <root /> element back off and convert the XmlDocument to an XElement.
        return XElement.Load(xmlDocument.DocumentElement!.FirstChild!.CreateNavigator()!.ReadSubtree());
    }
    
    // 1-
    void IInternalEncryptedXmlDecryptor.PerformPreDecryptionSetup(EncryptedXml encryptedXml)
    {
        // no-op
    }
        
    private class EncryptedXmlWithCertificateKeys : EncryptedXml
    {
        private readonly XmlKeyDecryptionOptions? _options;
        
        public EncryptedXmlWithCertificateKeys(
            XmlKeyDecryptionOptions? options, 
            XmlDocument document) : base(document)
        {
            _options = options;
        }
        
        public override byte[]? DecryptEncryptedKey(EncryptedKey encryptedKey)
        {
            if (_options != null && _options.KeyDecryptionCertificateCount > 0)
            {
                var keyInfoEnum = encryptedKey.KeyInfo?.GetEnumerator();
                if (keyInfoEnum == null)
                {
                    return null;
                }
                
                while (keyInfoEnum.MoveNext())
                {
                    if (!(keyInfoEnum.Current is KeyInfoX509Data kiX509Data))
                    {
                        continue;
                    }
                    
                    var key = GetKeyFromCert(encryptedKey, kiX509Data);
                    if (key != null)
                    {
                        return key;
                    }
                }
            }
            
            return base.DecryptEncryptedKey(encryptedKey);
        }
        
        private byte[]? GetKeyFromCert(EncryptedKey encryptedKey, KeyInfoX509Data keyInfo)
        {
            var certEnum = keyInfo.Certificates?.GetEnumerator();
            if (certEnum == null)
            {
                return null;
            }
            
            while (certEnum.MoveNext())
            {
                if (!(certEnum.Current is X509Certificate2 certInfo))
                {
                    continue;
                }
                
                if (_options == null || 
                    !_options.TryGetKeyDecryptionCertificates(certInfo, out var keyDecryptionCerts))
                {
                    continue;
                }
                
                foreach (var keyDecryptionCert in keyDecryptionCerts)
                {
                    if (!keyDecryptionCert.HasPrivateKey)
                    {
                        continue;
                    }
                    
                    using (var privateKey = keyDecryptionCert.GetRSAPrivateKey())
                    {
                        if (privateKey != null)
                        {
                            var useOAEP = encryptedKey.EncryptionMethod?.KeyAlgorithm == XmlEncRSAOAEPUrl;
                            return DecryptKey(encryptedKey.CipherData.CipherValue, privateKey, useOAEP);
                        }
                    }
                }
            }
            
            return null;
        }
    }
}

```

###### - xml key decryption options

```c#
internal class XmlKeyDecryptionOptions
{
    // cert 容器
    private readonly Dictionary<string, List<X509Certificate2>> _certs = 
        new Dictionary<string, List<X509Certificate2>>(StringComparer.Ordinal);
    
    public int KeyDecryptionCertificateCount => _certs.Count;
    
    // 方法- get cert
    public bool TryGetKeyDecryptionCertificates(
        X509Certificate2 certInfo, 
        [NotNullWhen(true)] out IReadOnlyList<X509Certificate2>? keyDecryptionCerts)
    {
        var key = GetKey(certInfo);
        var retVal = _certs.TryGetValue(key, out var keyDecryptionCertsRetVal);
        keyDecryptionCerts = keyDecryptionCertsRetVal;
        return retVal;
    }
    
    private string GetKey(X509Certificate2 cert) => cert.Thumbprint;
    
    // 方法- add cert
    public void AddKeyDecryptionCertificate(X509Certificate2 certificate)
    {
        var key = GetKey(certificate);
        if (!_certs.TryGetValue(key, out var certificates))
        {
            certificates = _certs[key] = new List<X509Certificate2>();
        }
        certificates.Add(certificate);
    }        
}

```

#### 3.7 key manager

```c#
// key manager
public interface IKeyManager
{    
    IKey CreateNewKey(DateTimeOffset activationDate, DateTimeOffset expirationDate);        
    IReadOnlyCollection<IKey> GetAllKeys();
    
    // Implementations are free to return 'CancellationToken.None' from this method.
    // Since this token is never guaranteed to fire, callers should still manually clear their caches at a regular interval.    
    CancellationToken GetCacheExpirationToken();
        
    // This method will not mutate existing IKey instances. 
    // After calling this method, all existing IKey instances should be discarded, and GetAllKeys should be called again.    
    void RevokeKey(Guid keyId, string? reason = null);            
    void RevokeAllKeys(DateTimeOffset revocationDate, string? reason = null);
}

// internal xml key manager
public interface IInternalXmlKeyManager
{    
    IKey CreateNewKey(
        Guid keyId, 
        DateTimeOffset creationDate, 
        DateTimeOffset activationDate, 
        DateTimeOffset expirationDate);        
    
    IAuthenticatedEncryptorDescriptor DeserializeDescriptorFromKeyElement(XElement keyElement);        
    
    void RevokeSingleKey(
        Guid keyId, 
        DateTimeOffset revocationDate, 
        string? reason);
}

```

##### 3.7.1 xml key manger

```c#
public sealed class XmlKeyManager : IKeyManager, IInternalXmlKeyManager
{
    // Used for serializing elements to persistent storage
    internal static readonly XName KeyElementName = "key";
    internal static readonly XName IdAttributeName = "id";
    internal static readonly XName VersionAttributeName = "version";
    internal static readonly XName CreationDateElementName = "creationDate";
    internal static readonly XName ActivationDateElementName = "activationDate";
    internal static readonly XName ExpirationDateElementName = "expirationDate";
    internal static readonly XName DescriptorElementName = "descriptor";
    internal static readonly XName DeserializerTypeAttributeName = "deserializerType";
    internal static readonly XName RevocationElementName = "revocation";
    internal static readonly XName RevocationDateElementName = "revocationDate";
    internal static readonly XName ReasonElementName = "reason";
    
    private const string RevokeAllKeysValue = "*";
    
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cacheExpirationTokenSource;    
    
    private readonly IActivator _activator;
    private readonly AlgorithmConfiguration _authenticatedEncryptorConfiguration;
    private readonly IEnumerable<IAuthenticatedEncryptorFactory> _encryptorFactories;
    private readonly IKeyEscrowSink? _keyEscrowSink;
    private readonly IDefaultKeyStorageDirectories _keyStorageDirectories;
    private readonly IInternalXmlKeyManager _internalKeyManager;
                   
    internal IXmlRepository KeyRepository { get; }
    internal IXmlEncryptor? KeyEncryptor { get; }    
        
#pragma warning disable PUB0001 // Pubternal type IActivator in public API
    public XmlKeyManager(
    	IOptions<KeyManagementOptions> keyManagementOptions, 
    	IActivator activator) : 
    		this(
                keyManagementOptions, 
                activator, 
                NullLoggerFactory.Instance)
    {
    }
        
    public XmlKeyManager(
        IOptions<KeyManagementOptions> keyManagementOptions, 
        IActivator activator, 
        ILoggerFactory loggerFactory) : 
    		this(
                keyManagementOptions, 
                activator, 
                loggerFactory, 
                DefaultKeyStorageDirectories.Instance)
    {
    }
#pragma warning disable PUB0001 // Pubternal type IActivator in public API
    
    internal XmlKeyManager(
        IOptions<KeyManagementOptions> keyManagementOptions,
        IActivator activator,
        ILoggerFactory loggerFactory,
        IInternalXmlKeyManager internalXmlKeyManager) : 
    		this(keyManagementOptions, activator, loggerFactory)
    {
        _internalKeyManager = internalXmlKeyManager;
    }
            
    internal XmlKeyManager(
        IOptions<KeyManagementOptions> keyManagementOptions,
        IActivator activator,
        ILoggerFactory loggerFactory,
        IDefaultKeyStorageDirectories keyStorageDirectories)
    {
        // 注入 logger
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<XmlKeyManager>();
        
        // 注入 default key storage directory
        _keyStorageDirectories = keyStorageDirectories ?? throw new ArgumentNullException(nameof(keyStorageDirectories));
        
        // 从 key management options 解析 xml repository, xml encryptor，
        var keyRepository = keyManagementOptions.Value.XmlRepository;
        var keyEncryptor = keyManagementOptions.Value.XmlEncryptor;
        
        // 如果 key repository 为 null，
        if (keyRepository == null)
        {
            // 而 key encryptor 不为 null，-> 抛出异常
            if (keyEncryptor != null)
            {
                throw new InvalidOperationException(
                    Resources.FormatXmlKeyManager_IXmlRepositoryNotFound(
                        nameof(IXmlRepository), 
                        nameof(IXmlEncryptor)));
            }
            // 如果 可以 encryptor 也为 null
            else
            {
                // 1- 解析 fallback key repository encryptor pair，=> key repository, key encryptor
                var keyRepositoryEncryptorPair = GetFallbackKeyRepositoryEncryptorPair();
                keyRepository = keyRepositoryEncryptorPair.Key;
                keyEncryptor = keyRepositoryEncryptorPair.Value;
            }
        }
        
        // 注入 key repostiry, key encryptor (xml repository & xml encryptor)
        KeyRepository = keyRepository;
        KeyEncryptor = keyEncryptor;
        
        // 从 key management options 解析 authenticated encryptor configuration
        _authenticatedEncryptorConfiguration = keyManagementOptions.Value.AuthenticatedEncryptorConfiguration!;
        // 从 key management options 解析 escrow sinks，-> aggregated escrow sink
        var escrowSinks = keyManagementOptions.Value.KeyEscrowSinks;
        _keyEscrowSink = escrowSinks.Count > 0 ? new AggregateKeyEscrowSink(escrowSinks) : null;
        // 注入 activator
        _activator = activator;
        // trigger expiration token
        TriggerAndResetCacheExpirationToken(suppressLogging: true);
        // 注入 internal key manager（没有则注入自身）
        _internalKeyManager = _internalKeyManager ?? this;
        
        // 从 key management options 解析 encryptor factory 并注入
        _encryptorFactories = keyManagementOptions.Value.AuthenticatedEncryptorFactories;
    }
    
    // 1- get fallback repo encryptor pair
    internal KeyValuePair<IXmlRepository, IXmlEncryptor?> GetFallbackKeyRepositoryEncryptorPair()
    {
        IXmlRepository? repository = null;
        IXmlEncryptor? encryptor = null;
        
        // 如果可以解析到 azure keys folder (%HOME% directory)        
        var azureWebSitesKeysFolder = _keyStorageDirectories.GetKeyStorageDirectoryForAzureWebSites();
        if (azureWebSitesKeysFolder != null)
        {
            _logger.UsingAzureAsKeyRepository(azureWebSitesKeysFolder.FullName);       
            // 创建 file system xml repository
            repository = new FileSystemXmlRepository(azureWebSitesKeysFolder, _loggerFactory);
        }
        // （否则，即不能解析到 azure keys folder）
        else
        {
            // 从 key storage directory 解析 (local) key storage directory
            var localAppDataKeysFolder = _keyStorageDirectories.GetKeyStorageDirectory();
            
            // 如果能够解析 (local) key storage directory，
            if (localAppDataKeysFolder != null)
            {
                // 如果是 windows 系统
                if (OSVersionUtil.IsWindows())
                {                    
                    Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)); 
                    // (* encryptor) 创建 dpapi xml encryptor
                    encryptor = new DpapiXmlEncryptor(
                        protectToLocalMachine: !DpapiSecretSerializerHelper.CanProtectToCurrentUserAccount(),
                        loggerFactory: _loggerFactory);
                }
                
                // （不是 windows 系统），(* repository) 创建 file system xml repository
                repository = new FileSystemXmlRepository(localAppDataKeysFolder, _loggerFactory);
                
                if (encryptor != null)
                {
                    _logger.UsingProfileAsKeyRepositoryWithDPAPI(localAppDataKeysFolder.FullName);
                }
                else
                {
                    _logger.UsingProfileAsKeyRepository(localAppDataKeysFolder.FullName);
                }
            }
            // （不能解析到 local key storage directory）
            else
            {                
                RegistryKey? regKeyStorageKey = null;
                
                // 如果是 windows 系统，
                if (OSVersionUtil.IsWindows())
                {
                    Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)); 
                    // 创建 reg key storageKey
                    regKeyStorageKey = RegistryXmlRepository.DefaultRegistryKey;
                }
                // regkeyStorageKey 不为 null（windows 系统），
                // -> 创建 dpai xml encryptor & registry xml repo
                if (regKeyStorageKey != null)
                {
                    Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)); 
                    regKeyStorageKey = RegistryXmlRepository.DefaultRegistryKey;
                                        
                    // If the user profile isn't available, we can protect using DPAPI (to machine).
                    encryptor = new DpapiXmlEncryptor(protectToLocalMachine: true, loggerFactory: _loggerFactory);
                    repository = new RegistryXmlRepository(regKeyStorageKey!, _loggerFactory);
                    
                    _logger.UsingRegistryAsKeyRepositoryWithDPAPI(regKeyStorageKey!.Name);
                }
                // （否则，即 regKeyStoragekey 为 null，即不是 windows 系统）
                // -> 创建 ephemeral xml repo（没有 encryptor）
                else
                {
                    // Final fallback - use an ephemeral repository since we don't know where else to go.
                    // This can only be used for development scenarios.
                    repository = new EphemeralXmlRepository(_loggerFactory);
                    
                    _logger.UsingEphemeralKeyRepository();
                }
            }
        }
        
        // encryptor 可能为 null
        return new KeyValuePair<IXmlRepository, IXmlEncryptor?>(repository, encryptor);
    }                                                        
    
    private sealed class AggregateKeyEscrowSink : IKeyEscrowSink
    {
        private readonly IList<IKeyEscrowSink> _sinks;
        
        public AggregateKeyEscrowSink(IList<IKeyEscrowSink> sinks)
        {
            _sinks = sinks;
        }
        
        public void Store(Guid keyId, XElement element)
        {
            foreach (var sink in _sinks)
            {
                sink.Store(keyId, element);
            }
        }
    }
    
    // trigger expiration token
    private void TriggerAndResetCacheExpirationToken(
        [CallerMemberName] string? opName = null, 
        bool suppressLogging = false)
    {
        if (!suppressLogging)
        {
            _logger.KeyCacheExpirationTokenTriggeredByOperation(opName!);
        }
        
        Interlocked.Exchange(
            ref _cacheExpirationTokenSource, 
            new CancellationTokenSource())?.Cancel();
    }    
}

```

###### 3.7.1.1 create new key

```c#
public sealed class XmlKeyManager : IKeyManager, IInternalXmlKeyManager
{
    //
    public IKey CreateNewKey(DateTimeOffset activationDate, DateTimeOffset expirationDate)
    {
        return _internalKeyManager.CreateNewKey(
            keyId: Guid.NewGuid(),
            creationDate: DateTimeOffset.UtcNow,
            activationDate: activationDate,
            expirationDate: expirationDate);
    }
    
    //
    IKey IInternalXmlKeyManager.CreateNewKey(
        Guid keyId, 
        DateTimeOffset creationDate, 
        DateTimeOffset activationDate, 
        DateTimeOffset expirationDate)
    {
        // <key id="{guid}" version="1">
        //   <creationDate>...</creationDate>
        //   <activationDate>...</activationDate>
        //   <expirationDate>...</expirationDate>
        //   <descriptor deserializerType="{typeName}">
        //     ...
        //   </descriptor>
        // </key>

        _logger.CreatingKey(keyId, creationDate, activationDate, expirationDate);
        
        // 由 encryptor configuration 创建 encryptor descriptor
        var newDescriptor = _authenticatedEncryptorConfiguration.CreateNewDescriptor()
            ?? CryptoUtil.Fail<IAuthenticatedEncryptorDescriptor>("CreateNewDescriptor returned null.");
        // 将 encryptor descriptor 导出 xml info
        var descriptorXmlInfo = newDescriptor.ExportToXml();

        _logger.DescriptorDeserializerTypeForKeyIs(keyId, descriptorXmlInfo.DeserializerType.AssemblyQualifiedName!);
        
        // build the <key> element
        var keyElement = new XElement(
            KeyElementName,
            new XAttribute(IdAttributeName, keyId),
            new XAttribute(VersionAttributeName, 1),
            new XElement(CreationDateElementName, creationDate),
            new XElement(ActivationDateElementName, activationDate),
            new XElement(ExpirationDateElementName, expirationDate),
            new XElement(
                DescriptorElementName,
                new XAttribute(
                    DeserializerTypeAttributeName, 
                    descriptorXmlInfo.DeserializerType.AssemblyQualifiedName!),
                descriptorXmlInfo.SerializedDescriptorElement));
        
        // 如果有 key escrow sink，-> 存储 <key id, key element> 到 escrow sink
        if (_keyEscrowSink != null)
        {
            _logger.KeyEscrowSinkFoundWritingKeyToEscrow(keyId);
        }
        else
        {
            _logger.NoKeyEscrowSinkFoundNotWritingKeyToEscrow(keyId);
        }
        _keyEscrowSink?.Store(keyId, keyElement);
        
        // 如果没有 key encryptor，-> 日志
        if (KeyEncryptor == null)
        {
            _logger.NoXMLEncryptorConfiguredKeyMayBePersistedToStorageInUnencryptedForm(keyId);
        }
        
        // 由 key encryptor 加密 key element
        var possiblyEncryptedKeyElement = KeyEncryptor?.EncryptIfNecessary(keyElement) ?? keyElement;
        
        // 由 key repository 存储 encrypted element
        // Persist it to the underlying repository and trigger the cancellation token.
        var friendlyName = string.Format(CultureInfo.InvariantCulture, "key-{0:D}", keyId);
        KeyRepository.StoreElement(possiblyEncryptedKeyElement, friendlyName);
        
        // trigger expiration token
        TriggerAndResetCacheExpirationToken();
        
        // And we're done!
        return new Key(
            keyId: keyId,
            creationDate: creationDate,
            activationDate: activationDate,
            expirationDate: expirationDate,
            descriptor: newDescriptor,
            encryptorFactories: _encryptorFactories);
    }
}

```

###### 3.7.1.2 get all keys

```c#
public sealed class XmlKeyManager : IKeyManager, IInternalXmlKeyManager
{        
    public IReadOnlyCollection<IKey> GetAllKeys()
    {
        // 从 key repository 解析 all element
        var allElements = KeyRepository.GetAllElements();
        
        // We aggregate all the information we read into three buckets
        Dictionary<Guid, KeyBase> keyIdToKeyMap = new Dictionary<Guid, KeyBase>();
        HashSet<Guid>? revokedKeyIds = null;
        DateTimeOffset? mostRecentMassRevocationDate = null;
        
        // 遍历 element
        foreach (var element in allElements)
        {
            // 如果是 key element，-> 创建 key 并注入 map
            if (element.Name == KeyElementName)
            {                
                // ProcessKeyElement can return null in the case of failure, and if this happens we'll move on.
                // Still need to throw if we see duplicate keys with the same id.
                var key = ProcessKeyElement(element);                
                if (key != null)
                {
                    if (keyIdToKeyMap.ContainsKey(key.KeyId))
                    {
                        throw Error.XmlKeyManager_DuplicateKey(key.KeyId);
                    }
                    keyIdToKeyMap[key.KeyId] = key;
                }
            }
            // 如果是 revocation element，-> 创建 revocation info 并注入 revoked key ids / most revocation date
            else if (element.Name == RevocationElementName)
            {
                var revocationInfo = ProcessRevocationElement(element);
                if (revocationInfo is Guid)
                {
                    // a single key was revoked
                    if (revokedKeyIds == null)
                    {
                        revokedKeyIds = new HashSet<Guid>();
                    }
                    revokedKeyIds.Add((Guid)revocationInfo);
                }
                else                
                {      
                    // all keys as of a certain date were revoked
                    DateTimeOffset thisMassRevocationDate = (DateTimeOffset)revocationInfo;
                    if (!mostRecentMassRevocationDate.HasValue || 
                        mostRecentMassRevocationDate < thisMassRevocationDate)
                    {
                        mostRecentMassRevocationDate = thisMassRevocationDate;
                    }
                }
            }
            // 都不是
            else
            {
                // Skip unknown elements.
                _logger.UnknownElementWithNameFoundInKeyringSkipping(element.Name);
            }
        }
        
        // Apply individual revocations
        if (revokedKeyIds != null)
        {
            foreach (Guid revokedKeyId in revokedKeyIds)
            {
                keyIdToKeyMap.TryGetValue(revokedKeyId, out var key);
                if (key != null)
                {
                    key.SetRevoked();
                    _logger.MarkedKeyAsRevokedInTheKeyring(revokedKeyId);
                }
                else
                {
                    _logger.TriedToProcessRevocationOfKeyButNoSuchKeyWasFound(revokedKeyId);
                }
            }
        }
        
        // Apply mass revocations
        if (mostRecentMassRevocationDate.HasValue)
        {
            foreach (var key in keyIdToKeyMap.Values)
            {
                // The contract of IKeyManager.RevokeAllKeys is that keys created *strictly before* the
                // revocation date are revoked. The system clock isn't very granular, and if this were
                // a less-than-or-equal check we could end up with the weird case where a revocation
                // immediately followed by a key creation results in a newly-created revoked key (since
                // the clock hasn't yet stepped).
                if (key.CreationDate < mostRecentMassRevocationDate)
                {
                    key.SetRevoked();
                    _logger.MarkedKeyAsRevokedInTheKeyring(key.KeyId);
                }
            }
        }
        
        // And we're finished!
        return keyIdToKeyMap.Values.ToList().AsReadOnly();
    }
    
    // a- process key element
    private KeyBase? ProcessKeyElement(XElement keyElement)
    {
        Debug.Assert(keyElement.Name == KeyElementName);
        
        try
        {
            // Read metadata and prepare the key for deferred instantiation
            Guid keyId = (Guid)keyElement.Attribute(IdAttributeName)!;
            DateTimeOffset creationDate = (DateTimeOffset)keyElement.Element(CreationDateElementName)!;
            DateTimeOffset activationDate = (DateTimeOffset)keyElement.Element(ActivationDateElementName)!;
            DateTimeOffset expirationDate = (DateTimeOffset)keyElement.Element(ExpirationDateElementName)!;
            
            _logger.FoundKey(keyId);
            
            return new DeferredKey(
                keyId: keyId,
                creationDate: creationDate,
                activationDate: activationDate,
                expirationDate: expirationDate,
                keyManager: this,
                keyElement: keyElement,
                encryptorFactories: _encryptorFactories);
        }
        catch (Exception ex)
        {
            WriteKeyDeserializationErrorToLog(ex, keyElement);
            
            // Don't include this key in the key ring
            return null;
        }
    }
    
    // b- process revocation element
    // returns a Guid (for specific keys) or a DateTimeOffset (for all keys created on or before a specific date)
    private object ProcessRevocationElement(XElement revocationElement)
    {
        Debug.Assert(revocationElement.Name == RevocationElementName);
        
        try
        {
            string keyIdAsString = (string)revocationElement.Element(KeyElementName)!.Attribute(IdAttributeName)!;
            if (keyIdAsString == RevokeAllKeysValue)
            {
                // this is a mass revocation of all keys as of the specified revocation date
                DateTimeOffset massRevocationDate = (DateTimeOffset)revocationElement.Element(RevocationDateElementName)!;
                _logger.FoundRevocationOfAllKeysCreatedPriorTo(massRevocationDate);
                return massRevocationDate;
            }
            else
            {
                // only one key is being revoked
                var keyId = XmlConvert.ToGuid(keyIdAsString);
                _logger.FoundRevocationOfKey(keyId);
                return keyId;
            }
        }
        catch (Exception ex)
        {
            // Any exceptions that occur are fatal - we don't want to continue if we cannot process
            // revocation information.
            _logger.ExceptionWhileProcessingRevocationElement(revocationElement, ex);
            throw;
        }
    }
}

```

###### 3.7.1.3 get cache expiration token

```c#
public sealed class XmlKeyManager : IKeyManager, IInternalXmlKeyManager
{
     public CancellationToken GetCacheExpirationToken()
    {
        Debug.Assert(
            _cacheExpirationTokenSource != null, 
            $"{nameof(TriggerAndResetCacheExpirationToken)} must have been called first.");    
         
        return Interlocked
            .CompareExchange<CancellationTokenSource?>(ref _cacheExpirationTokenSource, null, null)
            .Token;
    }
}
```

###### 3.7.1.4 revoke key

```c#
public sealed class XmlKeyManager : IKeyManager, IInternalXmlKeyManager
{
    // revoke key
    public void RevokeKey(Guid keyId, string? reason = null)
    {
        _internalKeyManager.RevokeSingleKey(
            keyId: keyId,
            revocationDate: DateTimeOffset.UtcNow,
            reason: reason);
    }
    
    // revoke single key
    void IInternalXmlKeyManager.RevokeSingleKey(Guid keyId, DateTimeOffset revocationDate, string? reason)
    {
        // <revocation version="1">
        //   <revocationDate>...</revocationDate>
        //   <key id="{guid}" />
        //   <reason>...</reason>
        // </revocation>
        
        _logger.RevokingKeyForReason(keyId, revocationDate, reason);
        
        var revocationElement = new XElement(
            RevocationElementName,
            new XAttribute(VersionAttributeName, 1),
            new XElement(RevocationDateElementName, revocationDate),
            new XElement(
                KeyElementName,
                new XAttribute(IdAttributeName, keyId)),
            new XElement(ReasonElementName, reason));
        
        // Persist it to the underlying repository and trigger the cancellation token
        var friendlyName = string.Format(CultureInfo.InvariantCulture, "revocation-{0:D}", keyId);
        KeyRepository.StoreElement(revocationElement, friendlyName);
        TriggerAndResetCacheExpirationToken();
    }
                  
    // revoke all keys
    public void RevokeAllKeys(DateTimeOffset revocationDate, string? reason = null)
    {
        // <revocation version="1">
        //   <revocationDate>...</revocationDate>
        //   <!-- ... -->
        //   <key id="*" />
        //   <reason>...</reason>
        // </revocation>
        
        _logger.RevokingAllKeysAsOfForReason(revocationDate, reason);
        
        var revocationElement = new XElement(
            RevocationElementName,
            new XAttribute(VersionAttributeName, 1),
            new XElement(RevocationDateElementName, revocationDate),
            new XComment(" All keys created before the revocation date are revoked. "),
            new XElement(
                KeyElementName,
                new XAttribute(IdAttributeName, RevokeAllKeysValue)),
            new XElement(ReasonElementName, reason));
        
        // Persist it to the underlying repository and trigger the cancellation token
        string friendlyName = "revocation-" + DateTimeOffsetToFilenameSafeString(revocationDate);
        KeyRepository.StoreElement(revocationElement, friendlyName);
        TriggerAndResetCacheExpirationToken();
    }
    
    // a- date time offset string
    private static string DateTimeOffsetToFilenameSafeString(DateTimeOffset dateTime)
    {
        // similar to the XML format for dates, but with punctuation stripped
        return dateTime.UtcDateTime.ToString("yyyyMMddTHHmmssFFFFFFFZ", CultureInfo.InvariantCulture);
    }                        
}

```

###### 3.7.1.5 deserialize descriptor from element

```c#
public sealed class XmlKeyManager : IKeyManager, IInternalXmlKeyManager
{
    IAuthenticatedEncryptorDescriptor IInternalXmlKeyManager.DeserializeDescriptorFromKeyElement(XElement keyElement)
    {
        try
        {
            // 解析 descriptor element（xelement）
            var descriptorElement = keyElement.Element(DescriptorElementName);
            // 从 descriptor element 解析 descriptor deserializer type name
            string descriptorDeserializerTypeName = (string)descriptorElement!.Attribute(DeserializerTypeAttributeName)!;
            
            // 解析 deserializer element
            var unencryptedInputToDeserializer = 
                descriptorElement.Elements().Single().DecryptElement(_activator);
            // 根据 descriptor deserializer type name 创建 deserializer
            var deserializerInstance = 
                _activator.CreateInstance<IAuthenticatedEncryptorDescriptorDeserializer>(descriptorDeserializerTypeName);
            // 使用 deserializer 逆序列化 descriptor（从 descriptor element）
            var descriptorInstance = 
                deserializerInstance.ImportFromXml(unencryptedInputToDeserializer);
            
            return descriptorInstance ?? CryptoUtil.Fail<IAuthenticatedEncryptorDescriptor>("ImportFromXml returned null.");
        }
        catch (Exception ex)
        {
            WriteKeyDeserializationErrorToLog(ex, keyElement);
            throw;
        }
    }
    
    private void WriteKeyDeserializationErrorToLog(Exception error, XElement keyElement)
    {
        // Ideally we'd suppress the error since it might contain sensitive information, but it would be too difficult for
        // an administrator to diagnose the issue if we hide this information. Instead we'll log the error to the error
        // log and the raw <key> element to the debug log. This works for our out-of-box XML decryptors since they don't
        // include sensitive information in the exception message.
        
        // write sanitized <key> element
        _logger.ExceptionWhileProcessingKeyElement(keyElement.WithoutChildNodes(), error);
        
        // write full <key> element
        _logger.AnExceptionOccurredWhileProcessingElementDebug(keyElement, error);
        
    }    
}

```

##### 3.7.2 key management options

```c#
public class KeyManagementOptions
{
    private static readonly TimeSpan _keyPropagationWindow = TimeSpan.FromDays(2);
    internal TimeSpan KeyPropagationWindow
    {
        get
        {            
            return _keyPropagationWindow;
        }
    }
    
    private static readonly TimeSpan _keyRingRefreshPeriod = TimeSpan.FromHours(24);
    internal TimeSpan KeyRingRefreshPeriod
    {
        get
        {            
            return _keyRingRefreshPeriod;
        }
    }
    
    private static readonly TimeSpan _maxServerClockSkew = TimeSpan.FromMinutes(5);
    internal TimeSpan MaxServerClockSkew
    {
        get
        {
            return _maxServerClockSkew;
        }
    }
    
    private TimeSpan _newKeyLifetime = TimeSpan.FromDays(90);                                           
    public TimeSpan NewKeyLifetime
    {
        get
        {
            return _newKeyLifetime;
        }
        set
        {
            if (value < TimeSpan.FromDays(7))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), 
                    Resources.KeyManagementOptions_MinNewKeyLifetimeViolated);
            }
            _newKeyLifetime = value;
        }
    }
            
    public AlgorithmConfiguration? AuthenticatedEncryptorConfiguration { get; set; }      
    public IList<IKeyEscrowSink> KeyEscrowSinks { get; } = new List<IKeyEscrowSink>();        
    public IXmlRepository? XmlRepository { get; set; }        
    public IXmlEncryptor? XmlEncryptor { get; set; }    
    public IList<IAuthenticatedEncryptorFactory> AuthenticatedEncryptorFactories { get; } = 
        new List<IAuthenticatedEncryptorFactory>();
        
    // If this value is 'false', the system will not generate new keys automatically.
    // The key ring must contain at least one active non-revoked key, otherwise calls to 
    // "IDataProtector.Protect(byte[])" may fail. 
	// The system may end up protecting payloads to expired keys if this property is set to 'false'.   
    public bool AutoGenerateKeys { get; set; } = true;
                
    public KeyManagementOptions()
    {
    }
       
    internal KeyManagementOptions(KeyManagementOptions other)
    {
        if (other != null)
        {
            AutoGenerateKeys = other.AutoGenerateKeys;
            _newKeyLifetime = other._newKeyLifetime;
            XmlEncryptor = other.XmlEncryptor;
            XmlRepository = other.XmlRepository;
            AuthenticatedEncryptorConfiguration = other.AuthenticatedEncryptorConfiguration;
            
            foreach (var keyEscrowSink in other.KeyEscrowSinks)
            {
                KeyEscrowSinks.Add(keyEscrowSink);
            }
            
            foreach (var encryptorFactory in other.AuthenticatedEncryptorFactories)
            {
                AuthenticatedEncryptorFactories.Add(encryptorFactory);
            }
        }
    }                                          
}

```

#### 3.8 key ring

##### 3.8.1 key ring

```c#
// 接口
public interface IKeyRing
{
    Guid DefaultKeyId { get; }
    IAuthenticatedEncryptor? DefaultAuthenticatedEncryptor { get; }        
        
    IAuthenticatedEncryptor? GetAuthenticatedEncryptorByKeyId(Guid keyId, out bool isRevoked);
}

// 实现
internal sealed class KeyRing : IKeyRing
{    
    // 容器
    private readonly Dictionary<Guid, KeyHolder> _keyIdToKeyHolderMap;
    
    // defautl key id
    public Guid DefaultKeyId { get; }
    
    // default encryptor
    private readonly KeyHolder _defaultKeyHolder;
    public IAuthenticatedEncryptor? DefaultAuthenticatedEncryptor
    {
        get
        {
            return _defaultKeyHolder.GetEncryptorInstance(out _);
        }
    }
           
    public KeyRing(IKey defaultKey, IEnumerable<IKey> allKeys)
    {
        // 创建 key holder map 
        _keyIdToKeyHolderMap = new Dictionary<Guid, KeyHolder>();
        
        // 注入 all keys
        foreach (IKey key in allKeys)
        {
            _keyIdToKeyHolderMap.Add(key.KeyId, new KeyHolder(key));
        }
        
        // 注入 default key
        if (!_keyIdToKeyHolderMap.ContainsKey(defaultKey.KeyId))
        {
            _keyIdToKeyHolderMap.Add(defaultKey.KeyId, new KeyHolder(defaultKey));
        }
        
        // 注入 default key holder
        DefaultKeyId = defaultKey.KeyId;
        _defaultKeyHolder = _keyIdToKeyHolderMap[DefaultKeyId];
    }
            
    public IAuthenticatedEncryptor? GetAuthenticatedEncryptorByKeyId(Guid keyId, out bool isRevoked)
    {
        isRevoked = false;
        // 从 key holder map 解析 key holder
        _keyIdToKeyHolderMap.TryGetValue(keyId, out var holder);
        // 从 holder 解析 encryptor
        return holder?.GetEncryptorInstance(out isRevoked);
    }
    
    // used for providing lazy activation of the authenticated encryptor instance
    private sealed class KeyHolder
    {
        private readonly IKey _key;
        private IAuthenticatedEncryptor? _encryptor;
        
        internal KeyHolder(IKey key)
        {
            _key = key;
        }
        
        internal IAuthenticatedEncryptor? GetEncryptorInstance(out bool isRevoked)
        {
            // simple double-check lock pattern
            // we can't use LazyInitializer<T> because we don't have a simple value factory
            IAuthenticatedEncryptor? encryptor = Volatile.Read(ref _encryptor);
            if (encryptor == null)
            {
                lock (this)
                {
                    encryptor = Volatile.Read(ref _encryptor);
                    if (encryptor == null)
                    {
                        encryptor = _key.CreateEncryptor();
                        Volatile.Write(ref _encryptor, encryptor);
                    }
                }
            }
            
            isRevoked = _key.IsRevoked;
            return encryptor;
        }
    }
}

```

##### 3.8.2 default key resolver

```c#
public interface IDefaultKeyResolver
{   
    // Locates the default key from the keyring.    
    DefaultKeyResolution ResolveDefaultKeyPolicy(DateTimeOffset now, IEnumerable<IKey> allKeys);
}

```

###### 3.8.2.1 default key resolution

```c#
public struct DefaultKeyResolution
{          
    public IKey? DefaultKey;            
    public IKey? FallbackKey;
       
    // 'true' if a new key should be persisted to the keyring, 'false' otherwise.
    // This value may be 'true' even if a valid default key was found.   
    public bool ShouldGenerateNewKey;
}

```

###### 3.8.2.2 实现

```c#
internal sealed class DefaultKeyResolver : IDefaultKeyResolver
{      
    private readonly TimeSpan _keyPropagationWindow;              
    private readonly TimeSpan _maxServerToServerClockSkew;
    private readonly ILogger _logger;  
    
    public DefaultKeyResolver(IOptions<KeyManagementOptions> keyManagementOptions) : 
    	this(keyManagementOptions, NullLoggerFactory.Instance)
    { 
    }
    
    public DefaultKeyResolver(
        IOptions<KeyManagementOptions> keyManagementOptions, 
        ILoggerFactory loggerFactory)
    {
        // 从 key management options 解析 propagation window
        _keyPropagationWindow = keyManagementOptions.Value.KeyPropagationWindow;
        // 从 key management options 解析 max server clock skew
        _maxServerToServerClockSkew = keyManagementOptions.Value.MaxServerClockSkew;
        _logger = loggerFactory.CreateLogger<DefaultKeyResolver>();
    }
    
    // 方法- resolve default key policy
    public DefaultKeyResolution ResolveDefaultKeyPolicy(DateTimeOffset now, IEnumerable<IKey> allKeys)
    {
        var retVal = default(DefaultKeyResolution);
        retVal.DefaultKey = FindDefaultKey(
            now, 
            allKeys, 
            out retVal.FallbackKey, 
            out retVal.ShouldGenerateNewKey);
        
        return retVal;
    }
    
    private IKey? FindDefaultKey(
        DateTimeOffset now, 
        IEnumerable<IKey> allKeys, 
        out IKey? fallbackKey, 
        out bool callerShouldGenerateNewKey)
    {
        // 过滤 all keys 的 activation date 没有过期的(+ clock skew)，
        // 按照 activation date 降序排序，取 first
        var preferredDefaultKey = (from key in allKeys
                                   where key.ActivationDate <= now + _maxServerToServerClockSkew
                                   orderby key.ActivationDate descending, key.KeyId ascending
                                   select key)
            					.FirstOrDefault();
        
        if (preferredDefaultKey != null)
        {
            _logger.ConsideringKeyWithExpirationDateAsDefaultKey(
                preferredDefaultKey.KeyId, 
                preferredDefaultKey.ExpirationDate);
            
            // 如果 preferred default key 是 revoked、expired、不能 can create authenticated encryptor，-> 置空
            // if the key has been revoked or is expired, it is no longer a candidate
            if (preferredDefaultKey.IsRevoked || 
                preferredDefaultKey.IsExpired(now) || 
                !CanCreateAuthenticatedEncryptor(preferredDefaultKey))
            {
                _logger.KeyIsNoLongerUnderConsiderationAsDefault(preferredDefaultKey.KeyId);
                preferredDefaultKey = null;
            }
        }
        
        // Only the key that has been most recently activated is eligible to be the preferred default,
        // and only if it hasn't expired or been revoked. This is intentional: generating a new key is
        // an implicit signal that we should stop using older keys (even if they're not revoked), so
        // activating a new key should permanently mark all older keys as non-preferred.
        
        if (preferredDefaultKey != null)
        {
            // 除了 preferred default key，没有 activation date 不是过期的、不是 expired、不是 revoked 的 key，-> 标记 true
            callerShouldGenerateNewKey = !allKeys.Any(key =>
                key.ActivationDate <= (preferredDefaultKey.ExpirationDate + maxServerToServerClockSkew) && 
                !key.IsExpired(now + _keyPropagationWindow) && 
                !key.IsRevoked);

            if (callerShouldGenerateNewKey)
            {
                _logger.DefaultKeyExpirationImminentAndRepository();
            }
            
            fallbackKey = null;
            return preferredDefaultKey;
        }
        
        // preferred default key 为 null，
        
        // If we got this far, the caller must generate a key now.
        // We should locate a fallback key, which is a key that can be used to protect payloads if the caller is configured 
        // not to generate a new key.
        // We should try to make sure the fallback key has propagated to all callers (so its creation date should be before 
        // the previous propagation period), and we cannot use revoked keys. The fallback key may be expired.
        fallbackKey = (from key in 
                       (from key in allKeys
                        where key.CreationDate <= now - _keyPropagationWindow
                        orderby key.CreationDate descending
                        select key).Concat(
                           from key in allKeys
                           orderby key.CreationDate ascending
                           select key)
                       where !key.IsRevoked && CanCreateAuthenticatedEncryptor(key)
                       select key).FirstOrDefault();

        _logger.RepositoryContainsNoViableDefaultKey();
        
        callerShouldGenerateNewKey = true;
        return null;
    }
    
    private bool CanCreateAuthenticatedEncryptor(IKey key)
    {
        try
        {
            var encryptorInstance = key.CreateEncryptor();
            if (encryptorInstance == null)
            {
                CryptoUtil.Fail<IAuthenticatedEncryptor>("CreateEncryptorInstance returned null.");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.KeyIsIneligibleToBeTheDefaultKeyBecauseItsMethodFailed(
                key.KeyId, 
                nameof(IKey.CreateEncryptor), 
                ex);
            
            return false;
        }
    }                
}

```

##### 3.8.3 key ring provider

```c#
/* This API supports infrastructure and is not intended to be used directly from your code. */
/* This API may change or be removed in future releases. */   

// key ring provider
public interface IKeyRingProvider
{        
    IKeyRing GetCurrentKeyRing();
}

// cacheable key ring provider
public interface ICacheableKeyRingProvider
{       
    CacheableKeyRing GetCacheableKeyRing(DateTimeOffset now);
}

```

###### 3.8.3.1 cacheable keyring

```c#
public sealed class CacheableKeyRing
{
    private readonly CancellationToken _expirationToken;
    
    internal DateTime ExpirationTimeUtc { get; }    
    internal IKeyRing KeyRing { get; }
    
    internal static bool IsValid([NotNullWhen(true)] CacheableKeyRing? keyRing, DateTime utcNow)
    {
        return keyRing != null && 
            !keyRing._expirationToken.IsCancellationRequested && 
            keyRing.ExpirationTimeUtc > utcNow;
    }
        
    internal CacheableKeyRing(
        CancellationToken expirationToken, 
        DateTimeOffset expirationTime, 
        IKey defaultKey, 
        IEnumerable<IKey> allKeys) : 
    		this(
                expirationToken, 
                expirationTime, 
                keyRing: new KeyRing(defaultKey, allKeys))
    {
    }
    
    internal CacheableKeyRing(
        CancellationToken expirationToken, 
        DateTimeOffset expirationTime, 
        IKeyRing keyRing)
    {
        _expirationToken = expirationToken;
        ExpirationTimeUtc = expirationTime.UtcDateTime;
        KeyRing = keyRing;
    }
                       
    internal CacheableKeyRing WithTemporaryExtendedLifetime(DateTimeOffset now)
    {
        var extension = TimeSpan.FromMinutes(2);
        return new CacheableKeyRing(CancellationToken.None, now + extension, KeyRing);
    }
}

```

###### 3.8.3.2 实现

```c#
internal sealed class KeyRingProvider : ICacheableKeyRingProvider, IKeyRingProvider
{
    private CacheableKeyRing? _cacheableKeyRing;
    private readonly object _cacheableKeyRingLockObj = new object();
    private readonly IDefaultKeyResolver _defaultKeyResolver;
    private readonly KeyManagementOptions _keyManagementOptions;
    private readonly IKeyManager _keyManager;
    private readonly ILogger _logger;
    
    // for testing
    internal ICacheableKeyRingProvider CacheableKeyRingProvider { get; set; }    
    internal DateTime AutoRefreshWindowEnd { get; set; }    
    internal bool InAutoRefreshWindow() => DateTime.UtcNow < AutoRefreshWindowEnd;
    
    public KeyRingProvider(
        IKeyManager keyManager,
        IOptions<KeyManagementOptions> keyManagementOptions,
        IDefaultKeyResolver defaultKeyResolver) : 
    		this(
                keyManager,
                keyManagementOptions,
                defaultKeyResolver,
                NullLoggerFactory.Instance)
    {
    }
    
    public KeyRingProvider(
        IKeyManager keyManager,
        IOptions<KeyManagementOptions> keyManagementOptions,
        IDefaultKeyResolver defaultKeyResolver,
        ILoggerFactory loggerFactory)
    {
        // clone so new instance is immutable
        _keyManagementOptions = new KeyManagementOptions(keyManagementOptions.Value); 
        _keyManager = keyManager;
        // 注册自身为 cacheable keyring provider
        CacheableKeyRingProvider = this;
        _defaultKeyResolver = defaultKeyResolver;
        _logger = loggerFactory.CreateLogger<KeyRingProvider>();
        
        // We will automatically refresh any unknown keys for 2 minutes see https://github.com/dotnet/aspnetcore/issues/3975
        AutoRefreshWindowEnd = DateTime.UtcNow.AddMinutes(2);
    }                
    
    // 方法- get current key ring
    public IKeyRing GetCurrentKeyRing()
    {
        return GetCurrentKeyRingCore(DateTime.UtcNow);
    }
    
    // 方法- refresh current key ring
    internal IKeyRing RefreshCurrentKeyRing()
    {
        return GetCurrentKeyRingCore(DateTime.UtcNow, forceRefresh: true);
    }
    
    internal IKeyRing GetCurrentKeyRingCore(DateTime utcNow, bool forceRefresh = false)
    {
        Debug.Assert(utcNow.Kind == DateTimeKind.Utc);
        
        // Can we return the cached keyring to the caller?
        CacheableKeyRing? existingCacheableKeyRing = null;
        
        // 如果没有标记 force refresh，-> 验证并返回 cacheable keyring
        if (!forceRefresh)
        {            
            existingCacheableKeyRing = Volatile.Read(ref _cacheableKeyRing);
            if (CacheableKeyRing.IsValid(existingCacheableKeyRing, utcNow))
            {
                return existingCacheableKeyRing.KeyRing;
            }
        }
        
        // （标记了 force refresh）
        
        // The cached keyring hasn't been created or must be refreshed. We'll allow one thread to update the keyring, 
        // and all other threads will continue to use the existing cached keyring while the first thread performs the update. 
        // There is an exception: if there is no usable existing cached keyring, all callers must block until the keyring exists.
        var acquiredLock = false;
        try
        {
            Monitor.TryEnter(
                _cacheableKeyRingLockObj, 
                (existingCacheableKeyRing != null) ? 0 : Timeout.Infinite, 
                ref acquiredLock);
            
            if (acquiredLock)
            {
                
                if (!forceRefresh)
                {
                    // This thread acquired the critical section and is responsible for updating the cached keyring. 
                    // But first, let's make sure that somebody didn't sneak in before us and update the keyring on our behalf.
                    existingCacheableKeyRing = Volatile.Read(ref _cacheableKeyRing);
                    if (CacheableKeyRing.IsValid(existingCacheableKeyRing, utcNow))
                    {
                        return existingCacheableKeyRing.KeyRing;
                    }
                    
                    if (existingCacheableKeyRing != null)
                    {
                        _logger.ExistingCachedKeyRingIsExpired();
                    }
                }
                
                // It's up to us to refresh the cached keyring.
                // This call is performed *under lock*.
                CacheableKeyRing newCacheableKeyRing;
                
                try
                {
                    // 用 cacheable keyring provider 创建 cacheable keyring
                    newCacheableKeyRing = CacheableKeyRingProvider.GetCacheableKeyRing(utcNow);
                }
                catch (Exception ex)
                {
                    if (existingCacheableKeyRing != null)
                    {
                        _logger.ErrorOccurredWhileRefreshingKeyRing(ex);
                    }
                    else
                    {
                        _logger.ErrorOccurredWhileReadingKeyRing(ex);
                    }
                    
                    // Failures that occur while refreshing the keyring are most likely transient, perhaps due to a
                    // temporary network outage. Since we don't want every subsequent call to result in failure, we'll
                    // create a new keyring object whose expiration is now + some short period of time (currently 2 min),
                    // and after this period has elapsed the next caller will try refreshing. If we don't have an
                    // existing keyring (perhaps because this is the first call), then there's nothing to extend, so
                    // each subsequent caller will keep going down this code path until one succeeds.
                    if (existingCacheableKeyRing != null)
                    {
                        Volatile.Write(
                            ref _cacheableKeyRing, 
                            existingCacheableKeyRing.WithTemporaryExtendedLifetime(utcNow));
                    }
                    
                    // The immediate caller should fail so that they can report the error up the chain. This makes it more likely
                    // that an administrator can see the error and react to it as appropriate. The caller can retry the operation
                    // and will probably have success as long as they fall within the temporary extension mentioned above.
                    throw;
                }
                
                Volatile.Write(ref _cacheableKeyRing, newCacheableKeyRing);
                return newCacheableKeyRing.KeyRing;
            }
            else
            {
                // We didn't acquire the critical section. This should only occur if we passed
                // zero for the Monitor.TryEnter timeout, which implies that we had an existing
                // (but outdated) keyring that we can use as a fallback.
                Debug.Assert(existingCacheableKeyRing != null);
                return existingCacheableKeyRing.KeyRing;
            }
        }
        finally
        {
            if (acquiredLock)
            {
                Monitor.Exit(_cacheableKeyRingLockObj);
            }
        }
    }
    
    CacheableKeyRing ICacheableKeyRingProvider.GetCacheableKeyRing(DateTimeOffset now)
    {
        // the entry point allows one recursive call
        return CreateCacheableKeyRingCore(now, keyJustAdded: null);
    }
        
    private CacheableKeyRing CreateCacheableKeyRingCore(DateTimeOffset now, IKey? keyJustAdded)
    {
        // 从 keymanager 解析 cache expiration token
        var cacheExpirationToken = _keyManager.GetCacheExpirationToken();
        // 从 keymanager 解析 all keys
        var allKeys = _keyManager.GetAllKeys();
        
        // 从 default key resolver 解析 default key policy
        var defaultKeyPolicy = _defaultKeyResolver.ResolveDefaultKeyPolicy(now, allKeys);
        // 如果 default key policy 没有标记 should generate new key，-> step2
        if (!defaultKeyPolicy.ShouldGenerateNewKey)
        {
            CryptoUtil.Assert(
                defaultKeyPolicy.DefaultKey != null, 
                "Expected to see a default key.");
            
            // 1-
            return CreateCacheableKeyRingCoreStep2(
                now, 
                cacheExpirationToken, 
                defaultKeyPolicy.DefaultKey, 
                allKeys);
        }
        
        // （default key policy 标记了 should generate new key）
        
        _logger.PolicyResolutionStatesThatANewKeyShouldBeAddedToTheKeyRing();
        
        // We shouldn't call CreateKey more than once, else we risk stack diving. This code path shouldn't
        // get hit unless there was an ineligible key with an activation date slightly later than the one we
        // just added. If this does happen, then we'll just use whatever key we can instead of creating
        // new keys endlessly, eventually falling back to the one we just added if all else fails.
        if (keyJustAdded != null)
        {
            // 解析 default key policy 的 default key (key just added as fallback)
            var keyToUse = defaultKeyPolicy.DefaultKey ?? defaultKeyPolicy.FallbackKey ?? keyJustAdded;
            // 1-
            return CreateCacheableKeyRingCoreStep2(now, cacheExpirationToken, keyToUse, allKeys);
        }
        
        // At this point, we know we need to generate a new key.
        
        // 如果 key management options 没有标记 auto generate keys
        if (!_keyManagementOptions.AutoGenerateKeys)
        {
            // 解析 default policy 的 default key
            var keyToUse = defaultKeyPolicy.DefaultKey ?? defaultKeyPolicy.FallbackKey;
            // 如果 key to use 为 null，-> 抛出异常
            if (keyToUse == null)
            {
                _logger.KeyRingDoesNotContainValidDefaultKey();
                throw new InvalidOperationException(Resources.KeyRingProvider_NoDefaultKey_AutoGenerateDisabled);
            }
            else
            {
                _logger.UsingFallbackKeyWithExpirationAsDefaultKey(keyToUse.KeyId, keyToUse.ExpirationDate);
                // 1-
                return CreateCacheableKeyRingCoreStep2(now, cacheExpirationToken, keyToUse, allKeys);
            }
        }
        
        // （key management options 标记了 auto generate keys）
        
        // 如果 default key policy 的 default key 为 null
        if (defaultKeyPolicy.DefaultKey == null)
        {
            // The case where there's no default key is the easiest scenario, since it means that we need to create a new key 
            // with immediate activation.
            var newKey = _keyManager.CreateNewKey(
                activationDate: now, 
                expirationDate: now + _keyManagementOptions.NewKeyLifetime);
            
            return CreateCacheableKeyRingCore(now, keyJustAdded: newKey); // recursively call
        }
        else
        {
            // If there is a default key, then the new key we generate should become active upon expiration of the default key. 
            // The new key lifetime is measured from the creation date (now), not the activation date.
            var newKey = _keyManager.CreateNewKey(
                activationDate: defaultKeyPolicy.DefaultKey.ExpirationDate, 
                expirationDate: now + _keyManagementOptions.NewKeyLifetime);
            
            return CreateCacheableKeyRingCore(now, keyJustAdded: newKey); // recursively call
        }
    }
    
    // 1-
    private CacheableKeyRing CreateCacheableKeyRingCoreStep2(
        DateTimeOffset now, 
        CancellationToken cacheExpirationToken, 
        IKey defaultKey, 
        IEnumerable<IKey> allKeys)
    {
        Debug.Assert(defaultKey != null);
        
        // Invariant: our caller ensures that CreateEncryptorInstance succeeded at least once
        Debug.Assert(defaultKey.CreateEncryptor() != null);
        
        _logger.UsingKeyAsDefaultKey(defaultKey.KeyId);
        
        var nextAutoRefreshTime = now + GetRefreshPeriodWithJitter(_keyManagementOptions.KeyRingRefreshPeriod);
        
        // The cached keyring should expire at the earliest of (default key expiration, next auto-refresh time).
        // Since the refresh period and safety window are not user-settable, we can guarantee that there's at
        // least one auto-refresh between the start of the safety window and the key's expiration date.
        // This gives us an opportunity to update the key ring before expiration, and it prevents multiple
        // servers in a cluster from trying to update the key ring simultaneously. Special case: if the default
        // key's expiration date is in the past, then we know we're using a fallback key and should disregard
        // its expiration date in favor of the next auto-refresh time.
        return new CacheableKeyRing(
            expirationToken: cacheExpirationToken,
            expirationTime: (defaultKey.ExpirationDate <= now) 
            	? nextAutoRefreshTime 
            	: Min(defaultKey.ExpirationDate, nextAutoRefreshTime),
            defaultKey: defaultKey,
            allKeys: allKeys);
    }
    
    private static TimeSpan GetRefreshPeriodWithJitter(TimeSpan refreshPeriod)
    {
        // We'll fudge the refresh period up to -20% so that multiple applications don't try to
        // hit a single repository simultaneously. For instance, if the refresh period is 1 hour,
        // we'll return a value in the vicinity of 48 - 60 minutes. We use the Random class since
        // we don't need a secure PRNG for this.
#if NET6_0_OR_GREATER
        var random = Random.Shared;
#else
        var random = new Random();
#endif
        return TimeSpan.FromTicks((long)(refreshPeriod.Ticks * (1.0d - (random.NextDouble() / 5))));
    }
    
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b)
    {
        return (a < b) ? a : b;
    }        
}

```

#### 3.9 registry policy resolver

```c#
internal interface IRegistryPolicyResolver
{
    RegistryPolicy? ResolvePolicy();
}

```

##### 3.9.1 registry policy

```c#
internal class RegistryPolicy
{
    public AlgorithmConfiguration? EncryptorConfiguration { get; }    
    public IEnumerable<IKeyEscrowSink> KeyEscrowSinks { get; }    
    public int? DefaultKeyLifetime { get; }
    
    public RegistryPolicy(
        AlgorithmConfiguration? configuration,
        IEnumerable<IKeyEscrowSink> keyEscrowSinks,
        int? defaultKeyLifetime)
    {
        EncryptorConfiguration = configuration;
        KeyEscrowSinks = keyEscrowSinks;
        DefaultKeyLifetime = defaultKeyLifetime;
    }        
}

```

##### 3.9.2 apply policy attribute

```c#
[AttributeUsage(
    AttributeTargets.Property, 
    AllowMultiple = false, 
    Inherited = false)]
internal sealed class ApplyPolicyAttribute : Attribute 
{
}

```

##### 3.9.3 实现

```c#
[SupportedOSPlatform("windows")]
internal sealed class RegistryPolicyResolver: IRegistryPolicyResolver
{
    private readonly Func<RegistryKey?> _getPolicyRegKey;
    private readonly IActivator _activator;
    
    public RegistryPolicyResolver(IActivator activator)
    {
        _getPolicyRegKey = () => 
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DotNetPackages\Microsoft.AspNetCore.DataProtection");
        _activator = activator;
    }
    
    internal RegistryPolicyResolver(RegistryKey policyRegKey, IActivator activator)
    {
        _getPolicyRegKey = () => policyRegKey;
        _activator = activator;
    }
    
    // 方法- resolve policy
    public RegistryPolicy? ResolvePolicy()
    {
        using (var registryKey = _getPolicyRegKey())
        {
            // fully evaluate enumeration while the reg key is open
            return ResolvePolicyCore(registryKey); 
        }
    }
    
    private RegistryPolicy? ResolvePolicyCore(RegistryKey? policyRegKey)
    {
        if (policyRegKey == null)
        {
            return null;
        }
        
        // Read the encryption options type: CNG-CBC, CNG-GCM, Managed
        AlgorithmConfiguration? configuration = null;
        
        // 从 registry 解析 encryption algorithm configuration
        var encryptionType = (string?)policyRegKey.GetValue("EncryptionType");
        if (String.Equals(encryptionType, "CNG-CBC", StringComparison.OrdinalIgnoreCase))
        {
            configuration = new CngCbcAuthenticatedEncryptorConfiguration();
        }
        else if (String.Equals(encryptionType, "CNG-GCM", StringComparison.OrdinalIgnoreCase))
        {
            configuration = new CngGcmAuthenticatedEncryptorConfiguration();
        }
        else if (String.Equals(encryptionType, "Managed", StringComparison.OrdinalIgnoreCase))
        {
            configuration = new ManagedAuthenticatedEncryptorConfiguration();
        }
        else if (!String.IsNullOrEmpty(encryptionType))
        {
            throw CryptoUtil.Fail("Unrecognized EncryptionType: " + encryptionType);
        }
        
        // 如果 encryption algorithm configuration 不为 null（即能解析）
        if (configuration != null)
        {
            // 1-
            PopulateOptions(configuration, policyRegKey);
        }
        
        // Read ancillary data
        
        var defaultKeyLifetime = (int?)policyRegKey.GetValue("DefaultKeyLifetime");
        // 2- 
        var keyEscrowSinks = ReadKeyEscrowSinks(policyRegKey).Select(item => _activator.CreateInstance<IKeyEscrowSink>(item));
        
        return new RegistryPolicy(configuration, keyEscrowSinks, defaultKeyLifetime);
    }
    
    // 1- populates an options object from values stored in the registry
    private static void PopulateOptions(object options, RegistryKey key)
    {
        // 遍历 options (encryptor configuration) 的 proper info
        foreach (PropertyInfo propInfo in options.GetType().GetProperties())
        {
            // 如果 property 标记了 apply policy 特性
            if (propInfo.IsDefined(typeof(ApplyPolicyAttribute)))
            {
                // 从 registry 解析 property value
                var valueFromRegistry = key.GetValue(propInfo.Name);
                if (valueFromRegistry != null)
                {
                    // 如果 property 是 string
                    if (propInfo.PropertyType == typeof(string))
                    {
                        propInfo.SetValue(
                            options, 
                            Convert.ToString(valueFromRegistry, CultureInfo.InvariantCulture));
                    }
                    // 如果 property 是 int
                    else if (propInfo.PropertyType == typeof(int))
                    {
                        propInfo.SetValue(
                            options, 
                            Convert.ToInt32(valueFromRegistry, CultureInfo.InvariantCulture));
                    }
                    // 如果 property 是 type
                    else if (propInfo.PropertyType == typeof(Type))
                    {
                        propInfo.SetValue(
                            options, 
                            Type.GetType(
                                Convert.ToString(valueFromRegistry, CultureInfo.InvariantCulture)!, 
                                throwOnError: true));
                    }
                    else
                    {
                        throw CryptoUtil.Fail("Unexpected type on property: " + propInfo.Name);
                    }
                }
            }
        }
    }
    
    // 2- 
    private static List<string> ReadKeyEscrowSinks(RegistryKey key)
    {
        // （以结果）
        var sinks = new List<string>();
        
        // 从 registry 解析 escrow sinks (string)
        // The format of this key is "type1; type2; ...".
        // We call Type.GetType to perform an eager check that the type exists.
        var sinksFromRegistry = (string?)key.GetValue("KeyEscrowSinks");
        if (sinksFromRegistry != null)
        {
            // 遍历 escrow sink string
            foreach (string sinkFromRegistry in sinksFromRegistry.Split(';'))
            {
                var candidate = sinkFromRegistry.Trim();
                if (!String.IsNullOrEmpty(candidate))
                {
                    // 如果 escrow sink 对应的 type 实现了 key escrow sink 接口，-> 注入 sinks（预结果）
                    typeof(IKeyEscrowSink).AssertIsAssignableFrom(Type.GetType(candidate, throwOnError: true)!);
                    sinks.Add(candidate);
                }
            }
        }
        
        return sinks;
    }        
}

```

### 4. data protector

#### 4.1 data protection provider

```c#
public interface IDataProtectionProvider
{        
    IDataProtector CreateProtector(string purpose);
}

```

##### 4.1.1 扩展方法

###### 4.1.1.1 create protector by provider

```c#
public static class DataProtectionCommonExtensions
{
    public static IDataProtector CreateProtector(
        this IDataProtectionProvider provider, 
        IEnumerable<string> purposes)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }        
        if (purposes == null)
        {
            throw new ArgumentNullException(nameof(purposes));
        }
        
        bool collectionIsEmpty = true;
        IDataProtectionProvider retVal = provider;
        foreach (string purpose in purposes)
        {
            if (purpose == null)
            {
                throw new ArgumentException(
                    Resources.DataProtectionExtensions_NullPurposesCollection, 
                    nameof(purposes));
            }
            // 逐级创建 protector
            retVal = retVal.CreateProtector(purpose) ?? CryptoUtil.Fail<IDataProtector>("CreateProtector returned null.");
            collectionIsEmpty = false;
        }
        
        if (collectionIsEmpty)
        {
            throw new ArgumentException(
                Resources.DataProtectionExtensions_NullPurposesCollection, 
                nameof(purposes));
        }
        
        // CreateProtector is supposed to return an instance of this interface
        Debug.Assert(retVal is IDataProtector); 
        return (IDataProtector)retVal;
    }
    
    //   
    public static IDataProtector CreateProtector(
        this IDataProtectionProvider provider, 
        string purpose, 
        params string[] subPurposes)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }        
        if (purpose == null)
        {
            throw new ArgumentNullException(nameof(purpose));
        }
        
        // The method signature isn't simply CreateProtector(this IDataProtectionProvider, params string[] purposes)
        // because we don't want the code provider.CreateProtector() [parameterless] to inadvertently compile.
        // The actual signature for this method forces at least one purpose to be provided at the call site.
        
        // 创建 purpose
        IDataProtector? protector = provider.CreateProtector(purpose);
        // 创建 sub purpose
        if (subPurposes != null && subPurposes.Length > 0)
        {
            protector = protector?.CreateProtector((IEnumerable<string>)subPurposes);
        }
        
        return protector ?? CryptoUtil.Fail<IDataProtector>("CreateProtector returned null.");
    }
}

```

###### 4.1.1.2 get provider from di services

```c#
public static class DataProtectionCommonExtensions
{
    public static IDataProtectionProvider GetDataProtectionProvider(this IServiceProvider services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
                
        var provider = (IDataProtectionProvider?)services.GetService(typeof(IDataProtectionProvider));
        if (provider == null)
        {
            throw new InvalidOperationException(
                Resources.FormatDataProtectionExtensions_NoService(typeof(IDataProtectionProvider).FullName));
        }
        
        return provider;
    }
}

```

###### 4.1.1.3 get protector from di services

```c#
public static class DataProtectionCommonExtensions
{
    public static IDataProtector GetDataProtector(
        this IServiceProvider services, 
        IEnumerable<string> purposes)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (purposes == null)
        {
            throw new ArgumentNullException(nameof(purposes));
        }
        
        // 从 di services 解析 protector provider，然后由 protector provider 创建 protector
        return services.GetDataProtectionProvider().CreateProtector(purposes);
    }
           
    public static IDataProtector GetDataProtector(
        this IServiceProvider services, 
        string purpose, 
        params string[] subPurposes)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (purpose == null)
        {
            throw new ArgumentNullException(nameof(purpose));
        }
        
        return services.GetDataProtectionProvider().CreateProtector(purpose, subPurposes);
    }
}

```

##### 4.1.2 ephemeral data protection provider

```c#
public sealed class EphemeralDataProtectionProvider : IDataProtectionProvider
{
    // 内联 keyring based protection provider
    private readonly KeyRingBasedDataProtectionProvider _dataProtectionProvider;
        
    public EphemeralDataProtectionProvider() : this (NullLoggerFactory.Instance)
    { 
    }
        
    public EphemeralDataProtectionProvider(ILoggerFactory loggerFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        // 创建 keyring provider，
        IKeyRingProvider keyringProvider;
        //   - 如果是 windows os，创建 ephemeral keyring with "cng gcm auth encryptor"
        if (OSVersionUtil.IsWindows())
        {            
            Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            // Fastest implementation: AES-256-GCM [CNG]
            keyringProvider = new EphemeralKeyRing<CngGcmAuthenticatedEncryptorConfiguration>(loggerFactory);
        }
        //   - 如果不是 windows os，创建 ephemeral key ring with "managed auth encryptor"
        else
        {
            // Slowest implementation: AES-256-CBC + HMACSHA256 [Managed]
            keyringProvider = new EphemeralKeyRing<ManagedAuthenticatedEncryptorConfiguration>(loggerFactory);
        }
        
        var logger = loggerFactory.CreateLogger<EphemeralDataProtectionProvider>();
        logger.UsingEphemeralDataProtectionProvider();
        
        // 创建 keyring based data protection provider
        _dataProtectionProvider = new KeyRingBasedDataProtectionProvider(keyringProvider, loggerFactory);
    }
    
    // 方法- create protector（调用内联 provider 的 create 方法）
    public IDataProtector CreateProtector(string purpose)
    {
        if (purpose == null)
        {
            throw new ArgumentNullException(nameof(purpose));
        }
        
        // just forward to the underlying provider
        return _dataProtectionProvider.CreateProtector(purpose);
    }
    
    private sealed class EphemeralKeyRing<T> : 
    	IKeyRing, 
    	IKeyRingProvider       
            where T : AlgorithmConfiguration, new()
    {
        public IAuthenticatedEncryptor? DefaultAuthenticatedEncryptor { get; }        
        public Guid DefaultKeyId { get; }
                        
        public EphemeralKeyRing(ILoggerFactory loggerFactory)
        {
            DefaultAuthenticatedEncryptor = GetDefaultEncryptor(loggerFactory);
        }
        
        private static IAuthenticatedEncryptor? GetDefaultEncryptor(ILoggerFactory loggerFactory)
        {
            var configuration = new T();
            if (configuration is CngGcmAuthenticatedEncryptorConfiguration cngConfiguration)
            {
                Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
                
                var descriptor = (CngGcmAuthenticatedEncryptorDescriptor)new T().CreateNewDescriptor();
                return new CngGcmAuthenticatedEncryptorFactory(loggerFactory)
                    .CreateAuthenticatedEncryptorInstance(
                    	descriptor.MasterKey,
                    	cngConfiguration);
            }
            else if (configuration is ManagedAuthenticatedEncryptorConfiguration managedConfiguration)
            {
                var descriptor = (ManagedAuthenticatedEncryptorDescriptor)new T().CreateNewDescriptor();
                return new ManagedAuthenticatedEncryptorFactory(loggerFactory)
                    .CreateAuthenticatedEncryptorInstance(
                    	descriptor.MasterKey,
                    	managedConfiguration);
            }
            
            return null;
        }
        
        public IAuthenticatedEncryptor? GetAuthenticatedEncryptorByKeyId(Guid keyId, out bool isRevoked)
        {
            isRevoked = false;
            return (keyId == default(Guid)) ? DefaultAuthenticatedEncryptor : null;
        }
        
        public IKeyRing GetCurrentKeyRing()
        {
            return this;
        }                
    }
}

```

##### 4.1.3 keyring based data protection provider

```c#
internal unsafe sealed class KeyRingBasedDataProtectionProvider : IDataProtectionProvider
{
    private readonly IKeyRingProvider _keyRingProvider;
    private readonly ILogger _logger;
    
    public KeyRingBasedDataProtectionProvider(
        IKeyRingProvider keyRingProvider, 
        ILoggerFactory loggerFactory)
    {
        // 注入 keyring provider
        _keyRingProvider = keyRingProvider;
        _logger = loggerFactory.CreateLogger<KeyRingBasedDataProtector>(); 
    }
    
    public IDataProtector CreateProtector(string purpose)
    {
        if (purpose == null)
        {
            throw new ArgumentNullException(nameof(purpose));
        }
        
        return new KeyRingBasedDataProtector(
            logger: _logger,
            keyRingProvider: _keyRingProvider,
            originalPurposes: null,
            newPurpose: purpose);
    }
}

```

#### 4.2 data protector

```c#
public interface IDataProtector : IDataProtectionProvider
{     
    byte[] Protect(byte[] plaintext);          
    byte[] Unprotect(byte[] protectedData);
}

```

##### 4.2.1 扩展方法

```c#
public static class DataProtectionCommonExtensions
{    
    // protect to string
    public static string Protect(this IDataProtector protector, string plaintext)
    {
        if (protector == null)
        {
            throw new ArgumentNullException(nameof(protector));
        }        
        if (plaintext == null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }
        
        try
        {
            byte[] plaintextAsBytes = EncodingUtil.SecureUtf8Encoding.GetBytes(plaintext);
            byte[] protectedDataAsBytes = protector.Protect(plaintextAsBytes);
            return WebEncoders.Base64UrlEncode(protectedDataAsBytes);
        }
        catch (Exception ex) when (ex.RequiresHomogenization())
        {
            // Homogenize exceptions to CryptographicException
            throw Error.CryptCommon_GenericError(ex);
        }
    }
    
    // unprotect to string
    public static string Unprotect(this IDataProtector protector, string protectedData)
    {
        if (protector == null)
        {
            throw new ArgumentNullException(nameof(protector));
        }        
        if (protectedData == null)
        {
            throw new ArgumentNullException(nameof(protectedData));
        }
        
        try
        {
            byte[] protectedDataAsBytes = WebEncoders.Base64UrlDecode(protectedData);
            byte[] plaintextAsBytes = protector.Unprotect(protectedDataAsBytes);
            return EncodingUtil.SecureUtf8Encoding.GetString(plaintextAsBytes);
        }
        catch (Exception ex) when (ex.RequiresHomogenization())
        {
            // Homogenize exceptions to CryptographicException
            throw Error.CryptCommon_GenericError(ex);
        }
    }
}

```

##### 4.2.2 time limited data protector

```c#
public interface ITimeLimitedDataProtector : IDataProtector
{    
    new ITimeLimitedDataProtector CreateProtector(string purpose);
        
    byte[] Protect(byte[] plaintext, DateTimeOffset expiration);       
    byte[] Unprotect(byte[] protectedData, out DateTimeOffset expiration);
}

```

###### 4.2.2.1 实现

```c#
internal sealed class TimeLimitedDataProtector : ITimeLimitedDataProtector
{
    private const string MyPurposeString = "Microsoft.AspNetCore.DataProtection.TimeLimitedDataProtector.v1";
    
    private readonly IDataProtector _innerProtector;
    private IDataProtector? _innerProtectorWithTimeLimitedPurpose; 	// created on-demand
    
    public TimeLimitedDataProtector(IDataProtector innerProtector)
    {
        _innerProtector = innerProtector;
    }
    
    public ITimeLimitedDataProtector CreateProtector(string purpose)
    {
        if (purpose == null)
        {
            throw new ArgumentNullException(nameof(purpose));
        }
        
        return new TimeLimitedDataProtector(_innerProtector.CreateProtector(purpose));
    }
            
    // 方法- protect
    public byte[] Protect(byte[] plaintext, DateTimeOffset expiration)
    {
        if (plaintext == null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }
        
        // We prepend the expiration time (as a 64-bit UTC tick count) to the unprotected data.
        byte[] plaintextWithHeader = new byte[checked(8 + plaintext.Length)];
        BitHelpers.WriteUInt64(plaintextWithHeader, 0, (ulong)expiration.UtcTicks);
        Buffer.BlockCopy(plaintext, 0, plaintextWithHeader, 8, plaintext.Length);
        
        // 解析 innert protector with time limit，调用其 protect 方法
        return GetInnerProtectorWithTimeLimitedPurpose().Protect(plaintextWithHeader);
    }
    
    private IDataProtector GetInnerProtectorWithTimeLimitedPurpose()
    {
        // thread-safe lazy init pattern with multi-execution and single publication
        
        // 返回 inner protector
        var retVal = Volatile.Read(ref _innerProtectorWithTimeLimitedPurpose);
        // 如果 inner protector 为 null，-> // 由 inner protector 创建 inner protector with time limit
        if (retVal == null)
        {            
            var newValue = _innerProtector.CreateProtector(MyPurposeString); 
            retVal = Interlocked.CompareExchange(ref _innerProtectorWithTimeLimitedPurpose, newValue, null) ?? newValue;
        }
        
        return retVal;
    }
        
    // 方法- unprotect
    public byte[] Unprotect(byte[] protectedData, out DateTimeOffset expiration)
    {
        if (protectedData == null)
        {
            throw new ArgumentNullException(nameof(protectedData));
        }
        
        return UnprotectCore(protectedData, DateTimeOffset.UtcNow, out expiration);
    }
    
    internal byte[] UnprotectCore(byte[] protectedData, DateTimeOffset now, out DateTimeOffset expiration)
    {
        if (protectedData == null)
        {
            throw new ArgumentNullException(nameof(protectedData));
        }
        
        try
        {
            // 解析 inner protector with time limit，调用其 unprotect 方法
            byte[] plaintextWithHeader = GetInnerProtectorWithTimeLimitedPurpose().Unprotect(protectedData);
            if (plaintextWithHeader.Length < 8)
            {
                // header isn't present
                throw new CryptographicException(Resources.TimeLimitedDataProtector_PayloadInvalid);
            }
            
            // Read expiration time back out of the payload
            ulong utcTicksExpiration = BitHelpers.ReadUInt64(plaintextWithHeader, 0);
            DateTimeOffset embeddedExpiration = new DateTimeOffset(checked((long)utcTicksExpiration), TimeSpan.Zero /* UTC */);
            
            // Are we expired?
            if (now > embeddedExpiration)
            {
                throw new CryptographicException(
                    Resources.FormatTimeLimitedDataProtector_PayloadExpired(embeddedExpiration));
            }
            
            // Not expired - split and return payload
            byte[] retVal = new byte[plaintextWithHeader.Length - 8];
            Buffer.BlockCopy(plaintextWithHeader, 8, retVal, 0, retVal.Length);
            expiration = embeddedExpiration;
            return retVal;
        }
        catch (Exception ex) when (ex.RequiresHomogenization())
        {
            // Homogenize all failures to CryptographicException
            throw new CryptographicException(Resources.CryptCommon_GenericError, ex);
        }
    }
    
    /*
    * EXPLICIT INTERFACE IMPLEMENTATIONS
    */
    
    IDataProtector IDataProtectionProvider.CreateProtector(string purpose)
    {
        if (purpose == null)
        {
            throw new ArgumentNullException(nameof(purpose));
        }
        
        return CreateProtector(purpose);
    }
    
    byte[] IDataProtector.Protect(byte[] plaintext)
    {
        if (plaintext == null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }
        
        // MaxValue essentially means 'no expiration'
        return Protect(plaintext, DateTimeOffset.MaxValue);
    }
    
    byte[] IDataProtector.Unprotect(byte[] protectedData)
    {
        if (protectedData == null)
        {
            throw new ArgumentNullException(nameof(protectedData));
        }
        
        DateTimeOffset expiration; // unused
        return Unprotect(protectedData, out expiration);
    }
}

```

###### 4.2.3.2 扩展方法

```c#
public static class DataProtectionAdvancedExtensions
{    
    // 转换 data protector => time limited protector
    public static ITimeLimitedDataProtector ToTimeLimitedDataProtector(this IDataProtector protector)
    {
        if (protector == null)
        {
            throw new ArgumentNullException(nameof(protector));
        }
        
        return (protector as ITimeLimitedDataProtector) ?? new TimeLimitedDataProtector(protector);
    }
    
    private sealed class TimeLimitedWrappingProtector : IDataProtector
    {
        public DateTimeOffset Expiration;
        
        // inner protector
        private readonly ITimeLimitedDataProtector _innerProtector;
        
        public TimeLimitedWrappingProtector(ITimeLimitedDataProtector innerProtector)
        {
            _innerProtector = innerProtector;
        }
        
        public IDataProtector CreateProtector(string purpose)
        {
            if (purpose == null)
            {
                throw new ArgumentNullException(nameof(purpose));
            }
            
            throw new NotImplementedException();
        }
        
        public byte[] Protect(byte[] plaintext)
        {
            if (plaintext == null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }
            
            return _innerProtector.Protect(plaintext, Expiration);
        }
        
        public byte[] Unprotect(byte[] protectedData)
        {
            if (protectedData == null)
            {
                throw new ArgumentNullException(nameof(protectedData));
            }
            
            return _innerProtector.Unprotect(protectedData, out Expiration);
        }
    }
    
    /* protect */
    
    // bytes & lifetime
    public static byte[] Protect(
        this ITimeLimitedDataProtector protector, 
        byte[] plaintext, 
        TimeSpan lifetime)
    {
        if (protector == null)
        {
            throw new ArgumentNullException(nameof(protector));
        }        
        if (plaintext == null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }
        
        return protector.Protect(plaintext, DateTimeOffset.UtcNow + lifetime);
    }
    // string & lifetime
    public static string Protect(
        this ITimeLimitedDataProtector protector, 
        string plaintext, 
        TimeSpan lifetime)
    {
        if (protector == null)
        {
            throw new ArgumentNullException(nameof(protector));
        }        
        if (plaintext == null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }
        
        return Protect(protector, plaintext, DateTimeOffset.Now + lifetime);
    }
      
    // string & expiration
    public static string Protect(
        this ITimeLimitedDataProtector protector, 
        string plaintext, 
        DateTimeOffset expiration)
    {
        if (protector == null)
        {
            throw new ArgumentNullException(nameof(protector));
        }
        
        if (plaintext == null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }
        
        var wrappingProtector = new TimeLimitedWrappingProtector(protector) { Expiration = expiration };
        return wrappingProtector.Protect(plaintext);
    }
        
    /* unprotect */
           
    // by expiration
    public static string Unprotect(
        this ITimeLimitedDataProtector protector, 
        string protectedData, 
        out DateTimeOffset expiration)
    {
        if (protector == null)
        {
            throw new ArgumentNullException(nameof(protector));
        }        
        if (protectedData == null)
        {
            throw new ArgumentNullException(nameof(protectedData));
        }
        
        var wrappingProtector = new TimeLimitedWrappingProtector(protector);
        string retVal = wrappingProtector.Unprotect(protectedData);
        expiration = wrappingProtector.Expiration;
        return retVal;
    }        
}

```

##### 4.2.4 persisted data protector

```c#
public interface IPersistedDataProtector : IDataProtector
{        
    byte[] DangerousUnprotect(
        byte[] protectedData, 
        bool ignoreRevocationErrors, 
        out bool requiresMigration, 
        out bool wasRevoked);
}

```

###### 4.2.4.1 实现 (keyring based protector)

```c#
internal unsafe sealed class KeyRingBasedDataProtector : IDataProtector, IPersistedDataProtector
{
    // This magic header identifies a v0 protected data blob. It's the high 28 bits of the SHA1 hash of
    // "Microsoft.AspNet.DataProtection.KeyManagement.KeyRingBasedDataProtector" [US-ASCII], big-endian.
    // The last nibble reserved for version information. There's also the nice property that "F0 C9" can never appear in 
    // a well-formed UTF8 sequence, so attempts to treat a protected payload as a UTF8-encoded string will fail, 
    // and devs can catch the mistake early.
    private const uint MAGIC_HEADER_V0 = 0x09F0C9F0;
    
    private readonly ILogger? _logger;
    
    private readonly IKeyRingProvider _keyRingProvider;
    private AdditionalAuthenticatedDataTemplate _aadTemplate;
    
    internal string[] Purposes { get; }
    
    public KeyRingBasedDataProtector(
        IKeyRingProvider keyRingProvider, 
        ILogger? logger, 
        string[]? originalPurposes, 
        string newPurpose)
    {
        Debug.Assert(keyRingProvider != null);
        _logger = logger; 
        
        // 合并 purpose
        Purposes = ConcatPurposes(originalPurposes, newPurpose);        
        // 注入 keyring provider
        _keyRingProvider = keyRingProvider;
        // 创建 additional auth data template
        _aadTemplate = new AdditionalAuthenticatedDataTemplate(Purposes);
    }
            
    private static string[] ConcatPurposes(string[]? originalPurposes, string newPurpose)
    {
        if (originalPurposes != null && originalPurposes.Length > 0)
        {
            var newPurposes = new string[originalPurposes.Length + 1];
            Array.Copy(originalPurposes, 0, newPurposes, 0, originalPurposes.Length);
            newPurposes[originalPurposes.Length] = newPurpose;
            return newPurposes;
        }
        else
        {
            return new string[] { newPurpose };
        }
    }
    
    // 
    public IDataProtector CreateProtector(string purpose)
    {
        if (purpose == null)
        {
            throw new ArgumentNullException(nameof(purpose));
        }
        
        return new KeyRingBasedDataProtector(
            logger: _logger,
            keyRingProvider: _keyRingProvider,
            originalPurposes: Purposes,
            newPurpose: purpose);
    }       
        
    private struct AdditionalAuthenticatedDataTemplate
    {
        private byte[] _aadTemplate;
        
        public AdditionalAuthenticatedDataTemplate(IEnumerable<string> purposes)
        {
            // matches MemoryStream.EnsureCapacity
            const int MEMORYSTREAM_DEFAULT_CAPACITY = 0x100; 
            var ms = new MemoryStream(MEMORYSTREAM_DEFAULT_CAPACITY);
            
            // additionalAuthenticatedData := 
            //   { magicHeader (32-bit) || keyId || purposeCount (32-bit) || (purpose)* }
            // purpose := 
            //   { utf8ByteCount (7-bit encoded) || utf8Text }
            
            using (var writer = new PurposeBinaryWriter(ms))
            {
                writer.WriteBigEndian(MAGIC_HEADER_V0);
                Debug.Assert(ms.Position == sizeof(uint));
                
                // skip over where the key id will be stored; we'll fill it in later
                var posPurposeCount = writer.Seek(sizeof(Guid), SeekOrigin.Current); 
                // skip over where the purposeCount will be stored; we'll fill it in later
                writer.Seek(sizeof(uint), SeekOrigin.Current); 
                
                uint purposeCount = 0;
                foreach (string purpose in purposes)
                {
                    Debug.Assert(purpose != null);
                    writer.Write(purpose); // prepends length as a 7-bit encoded integer
                    purposeCount++;
                }
                
                // Once we have written all the purposes, go back and fill in 'purposeCount'
                writer.Seek(checked((int)posPurposeCount), SeekOrigin.Begin);
                writer.WriteBigEndian(purposeCount);
            }
            
            _aadTemplate = ms.ToArray();
        }
        
        public byte[] GetAadForKey(Guid keyId, bool isProtecting)
        {
            // Multiple threads might be trying to read and write the _aadTemplate field simultaneously. 
            // We need to make sure all accesses to it are thread-safe.
            var existingTemplate = Volatile.Read(ref _aadTemplate);
            Debug.Assert(existingTemplate.Length >= sizeof(uint) /* MAGIC_HEADER */ + sizeof(Guid) /* keyId */);
            
            // If the template is already initialized to this key id, return it.
            // The caller will not mutate it.
            fixed (byte* pExistingTemplate = existingTemplate)
            {
                if (Read32bitAlignedGuid(&pExistingTemplate[sizeof(uint)]) == keyId)
                {
                    return existingTemplate;
                }
            }
            
            // Clone since we're about to make modifications.
            // If this is an encryption operation, we only ever encrypt to the default key,
            // so we should replace the existing template. This could occur after the protector
            // has already been created, such as when the underlying key ring has been modified.
            byte[] newTemplate = (byte[])existingTemplate.Clone();
            fixed (byte* pNewTemplate = newTemplate)
            {
                Write32bitAlignedGuid(&pNewTemplate[sizeof(uint)], keyId);
                if (isProtecting)
                {
                    Volatile.Write(ref _aadTemplate, newTemplate);
                }
                return newTemplate;
            }
        }
        
        private sealed class PurposeBinaryWriter : BinaryWriter
        {
            public PurposeBinaryWriter(MemoryStream stream) : 
            	base(stream, EncodingUtil.SecureUtf8Encoding, leaveOpen: true) 
            {
            }
            
            // Writes a big-endian 32-bit integer to the underlying stream.
            public void WriteBigEndian(uint value)
            {
                var outStream = BaseStream; // property accessor also performs a flush
                outStream.WriteByte((byte)(value >> 24));
                outStream.WriteByte((byte)(value >> 16));
                outStream.WriteByte((byte)(value >> 8));
                outStream.WriteByte((byte)(value));
            }
        }
    }
        
    private static string JoinPurposesForLog(IEnumerable<string> purposes)
    {
        return "(" + String.Join(", ", purposes.Select(p => "'" + p + "'")) + ")";
    }    
}

```

###### - create protector

```c#
internal unsafe sealed class KeyRingBasedDataProtector : IDataProtector, IPersistedDataProtector
{
    
}
```

###### - protect

```c#
internal unsafe sealed class KeyRingBasedDataProtector : IDataProtector, IPersistedDataProtector
{
    public byte[] Protect(byte[] plaintext)
    {
        if (plaintext == null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }
        
        try
        {            
            // 从 keyring provider 解析 current keyring
            var currentKeyRing = _keyRingProvider.GetCurrentKeyRing();
            // 从 current keyring 解析 default keyid
            var defaultKeyId = currentKeyRing.DefaultKeyId;
            // 从 current keyring 解析 default encryptor
            var defaultEncryptorInstance = currentKeyRing.DefaultAuthenticatedEncryptor;
            CryptoUtil.Assert(
                defaultEncryptorInstance != null, 
                "defaultEncryptorInstance != null");
            
            if (_logger.IsDebugLevelEnabled())
            {
                _logger.PerformingProtectOperationToKeyWithPurposes(
                    defaultKeyId, 
                    JoinPurposesForLog(Purposes));
            }
            
            // We'll need to apply the default key id to the template if it hasn't already been applied.
            // If the default key id has been updated since the last call to Protect, also write back the updated template.
            var aad = _aadTemplate.GetAadForKey(defaultKeyId, isProtecting: true);
            
            // We allocate a 20-byte pre-buffer so that we can inject the magic header and key id into the return value.
            var retVal = defaultEncryptorInstance.Encrypt(
                plaintext: new ArraySegment<byte>(plaintext),
                additionalAuthenticatedData: new ArraySegment<byte>(aad),
                preBufferSize: (uint)(sizeof(uint) + sizeof(Guid)),
                postBufferSize: 0);   
            
            CryptoUtil.Assert(
                retVal != null && retVal.Length >= sizeof(uint) + sizeof(Guid), 
                "retVal != null && retVal.Length >= sizeof(uint) + sizeof(Guid)");
            
            // At this point: retVal := { 000..000 || encryptorSpecificProtectedPayload },
            // where 000..000 is a placeholder for our magic header and key id.
            
            // Write out the magic header and key id
            fixed (byte* pbRetVal = retVal)
            {
                // 1-
                WriteBigEndianInteger(pbRetVal, MAGIC_HEADER_V0);
                // 2-
                Write32bitAlignedGuid(&pbRetVal[sizeof(uint)], defaultKeyId);
            }
            
            // At this point, retVal := { magicHeader || keyId || encryptorSpecificProtectedPayload }
            // And we're done!
            return retVal;
        }
        catch (Exception ex) when (ex.RequiresHomogenization())
        {
            // homogenize all errors to CryptographicException
            throw Error.Common_EncryptionFailed(ex);
        }
    }        
    
    // 1-
    private static void WriteBigEndianInteger(byte* ptr, uint value)
    {
        ptr[0] = (byte)(value >> 24);
        ptr[1] = (byte)(value >> 16);
        ptr[2] = (byte)(value >> 8);
        ptr[3] = (byte)(value);
    }
    
    // 2-
    private static void Write32bitAlignedGuid(void* ptr, Guid value)
    {
        Debug.Assert((long)ptr % 4 == 0);
        
        ((int*)ptr)[0] = ((int*)&value)[0];
        ((int*)ptr)[1] = ((int*)&value)[1];
        ((int*)ptr)[2] = ((int*)&value)[2];
        ((int*)ptr)[3] = ((int*)&value)[3];
    }            
}

```

###### - unprotect

```c#
internal unsafe sealed class KeyRingBasedDataProtector : IDataProtector, IPersistedDataProtector
{
    // unprotect
    public byte[] Unprotect(byte[] protectedData)
    {
        if (protectedData == null)
        {
            throw new ArgumentNullException(nameof(protectedData));
        }
        
        // Argument checking will be done by the callee
        bool requiresMigration, wasRevoked; // unused
        return DangerousUnprotect(
            protectedData,
            ignoreRevocationErrors: false,
            requiresMigration: out requiresMigration,
            wasRevoked: out wasRevoked);
    }
    
    // dangerous unprotect, allows decrypting payloads whose keys have been revoked
    public byte[] DangerousUnprotect(
        byte[] protectedData, 
        bool ignoreRevocationErrors, 
        out bool requiresMigration, 
        out bool wasRevoked)
    {
        // argument & state checking
        if (protectedData == null)
        {
            throw new ArgumentNullException(nameof(protectedData));
        }
        
        UnprotectStatus status;
        var retVal = UnprotectCore(protectedData, ignoreRevocationErrors, status: out status);
        
        requiresMigration = (status != UnprotectStatus.Ok);
        wasRevoked = (status == UnprotectStatus.DecryptionKeyWasRevoked);
        
        return retVal;
    }
    
    private enum UnprotectStatus
    {
        Ok,
        DefaultEncryptionKeyChanged,
        DecryptionKeyWasRevoked
    }
    
    private byte[] UnprotectCore(
        byte[] protectedData, 
        bool allowOperationsOnRevokedKeys, 
        out UnprotectStatus status)
    {
        Debug.Assert(protectedData != null);
        
        try
        {
            // argument & state checking
            if (protectedData.Length < sizeof(uint) /* magic header */ + sizeof(Guid) /* key id */)
            {
                // payload must contain at least the magic header and key id
                throw Error.ProtectionProvider_BadMagicHeader();
            }
            
            // Need to check that protectedData := 
            //   { magicHeader || keyId || encryptorSpecificProtectedPayload }
            
            // Parse the payload version number and key id.
            uint magicHeaderFromPayload;
            Guid keyIdFromPayload;
            fixed (byte* pbInput = protectedData)
            {
                // 1-
                magicHeaderFromPayload = ReadBigEndian32BitInteger(pbInput);
                // 2-
                keyIdFromPayload = Read32bitAlignedGuid(&pbInput[sizeof(uint)]);
            }
            
            // Are the magic header and version information correct?
            int payloadVersion;
            // 3-
            if (!TryGetVersionFromMagicHeader(magicHeaderFromPayload, out payloadVersion))
            {
                throw Error.ProtectionProvider_BadMagicHeader();
            }
            else if (payloadVersion != 0)
            {
                throw Error.ProtectionProvider_BadVersion();
            }
            
            if (_logger.IsDebugLevelEnabled())
            {
                _logger.PerformingUnprotectOperationToKeyWithPurposes(
                    keyIdFromPayload, 
                    JoinPurposesForLog(Purposes));
            }
                        
            bool keyWasRevoked;
            // 从 keyring provider 解析 current keyring
            var currentKeyRing = _keyRingProvider.GetCurrentKeyRing();
            // 从 current keyring 解析 keyid from payload 对应的 encryptor，并判断 key revoked
            var requestedEncryptor = currentKeyRing.GetAuthenticatedEncryptorByKeyId(
                keyIdFromPayload, 
                out keyWasRevoked);
            
            // 如果 encryptor 为 null
            if (requestedEncryptor == null)
            {
                // refresh keyring => re-get encryptor & key revoked
                if (_keyRingProvider is KeyRingProvider provider && provider.InAutoRefreshWindow())
                {                    
                    currentKeyRing = provider.RefreshCurrentKeyRing();
                    requestedEncryptor = currentKeyRing.GetAuthenticatedEncryptorByKeyId(
                        keyIdFromPayload, 
                        out keyWasRevoked);
                }
                
                // 如果 encryptor 还是为 null，-> 抛出异常
                if (requestedEncryptor == null)
                {
                    if (_logger.IsTraceLevelEnabled())
                    {
                        _logger.KeyWasNotFoundInTheKeyRingUnprotectOperationCannotProceed(keyIdFromPayload);
                    }
                    throw Error.Common_KeyNotFound(keyIdFromPayload);
                }
            }
            
            // Do we need to notify the caller that they should reprotect the data?
            status = UnprotectStatus.Ok;
            if (keyIdFromPayload != currentKeyRing.DefaultKeyId)
            {
                status = UnprotectStatus.DefaultEncryptionKeyChanged;
            }
            
            // Do we need to notify the caller that this key was revoked?
            if (keyWasRevoked)
            {
                if (allowOperationsOnRevokedKeys)
                {
                    if (_logger.IsDebugLevelEnabled())
                    {
                        _logger.KeyWasRevokedCallerRequestedUnprotectOperationProceedRegardless(keyIdFromPayload);
                    }
                    status = UnprotectStatus.DecryptionKeyWasRevoked;
                }
                else
                {
                    if (_logger.IsDebugLevelEnabled())
                    {
                        _logger.KeyWasRevokedUnprotectOperationCannotProceed(keyIdFromPayload);
                    }
                    throw Error.Common_KeyRevoked(keyIdFromPayload);
                }
            }
            
            // Perform the decryption operation.
            ArraySegment<byte> ciphertext = new ArraySegment<byte>(
                protectedData, 
                sizeof(uint) + sizeof(Guid), 
                protectedData.Length - (sizeof(uint) + sizeof(Guid))); // chop off magic header + encryptor id
            
            ArraySegment<byte> additionalAuthenticatedData = 
                new ArraySegment<byte>(_aadTemplate.GetAadForKey(keyIdFromPayload, isProtecting: false));
            
            // At this point, cipherText := { encryptorSpecificPayload },
            // so all that's left is to invoke the decryption routine directly.
            
            // 使用 request encryptor（解析得到）的 decrypt 方法
            return requestedEncryptor.Decrypt(ciphertext, additionalAuthenticatedData)
                ?? CryptoUtil.Fail<byte[]>("IAuthenticatedEncryptor.Decrypt returned null.");
        }
        catch (Exception ex) when (ex.RequiresHomogenization())
        {
            // homogenize all failures to CryptographicException
            throw Error.DecryptionFailed(ex);
        }
    }
    
    // 1- 
    private static uint ReadBigEndian32BitInteger(byte* ptr)
    {
        return ((uint)ptr[0] << 24) |
            ((uint)ptr[1] << 16) | 
            ((uint)ptr[2] << 8) | 
            ((uint)ptr[3]);
    }
    
    // 2-
    private static Guid Read32bitAlignedGuid(void* ptr)
    {
        Debug.Assert((long)ptr % 4 == 0);
        
        Guid retVal;
        ((int*)&retVal)[0] = ((int*)ptr)[0];
        ((int*)&retVal)[1] = ((int*)ptr)[1];
        ((int*)&retVal)[2] = ((int*)ptr)[2];
        ((int*)&retVal)[3] = ((int*)ptr)[3];
        return retVal;
    }        
            
    // 3-
    private static bool TryGetVersionFromMagicHeader(uint magicHeader, out int version)
    {
        const uint MAGIC_HEADER_VERSION_MASK = 0xFU;
        if ((magicHeader & ~MAGIC_HEADER_VERSION_MASK) == MAGIC_HEADER_V0)
        {
            version = (int)(magicHeader & MAGIC_HEADER_VERSION_MASK);
            return true;
        }
        else
        {
            version = default(int);
            return false;
        }
    }
}

```



#### 4.3 data protection builder

```c#
// 接口
public interface IDataProtectionBuilder
{     
    IServiceCollection Services { get; }
}

// 实现
internal class DataProtectionBuilder : IDataProtectionBuilder
{
    public IServiceCollection Services { get; }
    
    public DataProtectionBuilder(IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        Services = services;
    }    
}

```

##### 4.3.1 扩展方法 - key management

###### 4.3.1.1 config key management options

```c#
public static class DataProtectionBuilderExtensions
{
    public static IDataProtectionBuilder AddKeyManagementOptions(
        this IDataProtectionBuilder builder, 
        Action<KeyManagementOptions> setupAction)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        builder.Services.Configure(setupAction);
        return builder;
    }
}

```

###### 4.3.1.2 disable auto generation

```c#
public static class DataProtectionBuilderExtensions
{
    public static IDataProtectionBuilder DisableAutomaticKeyGeneration(this IDataProtectionBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        builder.Services.Configure<KeyManagementOptions>(
            options =>
            {
                options.AutoGenerateKeys = false;
            });
        
        return builder;
    }
}

```

###### 4.3.1.3 set default key lifetime

```c#
public static class DataProtectionBuilderExtensions
{
    public static IDataProtectionBuilder SetDefaultKeyLifetime(
        this IDataProtectionBuilder builder, 
        TimeSpan lifetime)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (lifetime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(Resources.FormatLifetimeMustNotBeNegative(nameof(lifetime)));
        }
        
        builder.Services.Configure<KeyManagementOptions>(
            options =>
            {
                options.NewKeyLifetime = lifetime;
            });
        
        return builder;
    }
}

```

###### 4.3.1.4 add key escrow sink

```c#
public static class DataProtectionBuilderExtensions
{
    // add escrow sink instance
    public static IDataProtectionBuilder AddKeyEscrowSink(
        this IDataProtectionBuilder builder, 
        IKeyEscrowSink sink)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (sink == null)
        {
            throw new ArgumentNullException(nameof(sink));
        }
        
        builder.Services.Configure<KeyManagementOptions>(options =>
            {
                options.KeyEscrowSinks.Add(sink);
            });
        
        return builder;
    }
    
    // by escrow sink type（type 需要注册在 di 中）
    public static IDataProtectionBuilder AddKeyEscrowSink<TImplementation>(this IDataProtectionBuilder builder)       
        where TImplementation : class, IKeyEscrowSink
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
            services =>
            {
                var implementationInstance = ervices.GetRequiredService<TImplementation>();
                return new ConfigureOptions<KeyManagementOptions>(
                    options =>
                    {
                        options.KeyEscrowSinks.Add(implementationInstance);
                    });
            });
        
        return builder;
    }
    
    // by escrow sink factory
    public static IDataProtectionBuilder AddKeyEscrowSink(
        this IDataProtectionBuilder builder, 
        Func<IServiceProvider, IKeyEscrowSink> factory)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
            services =>
            {
                var instance = factory(services);
                return new ConfigureOptions<KeyManagementOptions>(
                    options =>
                    {
                        options.KeyEscrowSinks.Add(instance);
                    });
            });
        
        return builder;
    }
}

```

###### 4.3.1.5 config key repository

```c#
public static class DataProtectionBuilderExtensions
{
    // file xml repo
    public static IDataProtectionBuilder PersistKeysToFileSystem(
        this IDataProtectionBuilder builder, 
        DirectoryInfo directory)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (directory == null)
        {
            throw new ArgumentNullException(nameof(directory));
        }
        
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
            services =>
            {
                var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                return new ConfigureOptions<KeyManagementOptions>(options =>
                {
                    options.XmlRepository = new FileSystemXmlRepository(directory, loggerFactory);
                });
            });
        
        return builder;
    }
    
    // windows registry repo
    [SupportedOSPlatform("windows")]
    public static IDataProtectionBuilder PersistKeysToRegistry(
        this IDataProtectionBuilder builder, 
        RegistryKey registryKey)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (registryKey == null)
        {
            throw new ArgumentNullException(nameof(registryKey));
        }
        
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
            services =>
            {
                var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                return new ConfigureOptions<KeyManagementOptions>(
                    options =>
                    {
                        options.XmlRepository = new RegistryXmlRepository(registryKey, loggerFactory);
                    });
            });
        
        return builder;
    }
}

```

###### 4.3.1.6 config key encryptor

```c#
public static class DataProtectionBuilderExtensions
{
    // dpapi encryptor
    [SupportedOSPlatform("windows")]
    public static IDataProtectionBuilder ProtectKeysWithDpapi(this IDataProtectionBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        return builder.ProtectKeysWithDpapi(protectToLocalMachine: false);
    }
    
    [SupportedOSPlatform("windows")]
    public static IDataProtectionBuilder ProtectKeysWithDpapi(
        this IDataProtectionBuilder builder, 
        bool protectToLocalMachine)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
            services =>
            {
                var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                return new ConfigureOptions<KeyManagementOptions>(
                    options =>
                    {
                        CryptoUtil.AssertPlatformIsWindows();
                        options.XmlEncryptor = new DpapiXmlEncryptor(protectToLocalMachine, loggerFactory);
                    });
            });
        
        return builder;
    }
    
    // dpapi ng
    [SupportedOSPlatform("windows")]
    public static IDataProtectionBuilder ProtectKeysWithDpapiNG(this IDataProtectionBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        return builder.ProtectKeysWithDpapiNG(
            protectionDescriptorRule: DpapiNGXmlEncryptor.GetDefaultProtectionDescriptorString(),
            flags: DpapiNGProtectionDescriptorFlags.None);
    }
    
    [SupportedOSPlatform("windows")]
    public static IDataProtectionBuilder ProtectKeysWithDpapiNG(
        this IDataProtectionBuilder builder, 
        string protectionDescriptorRule, 
        DpapiNGProtectionDescriptorFlags flags)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (protectionDescriptorRule == null)
        {
            throw new ArgumentNullException(nameof(protectionDescriptorRule));
        }
        
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
            services =>
            {
                var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                return new ConfigureOptions<KeyManagementOptions>(
                    options =>
                    {
                        CryptoUtil.AssertPlatformIsWindows8OrLater();
                        options.XmlEncryptor = new DpapiNGXmlEncryptor(protectionDescriptorRule, flags, loggerFactory);
                    });
            });
        
        return builder;
    }
    
    // cert encryptor
    public static IDataProtectionBuilder ProtectKeysWithCertificate(
        this IDataProtectionBuilder builder, 
        X509Certificate2 certificate)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (certificate == null)
        {
            throw new ArgumentNullException(nameof(certificate));
        }
        
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
            services =>            
            {
                var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                return new ConfigureOptions<KeyManagementOptions>(options =>
                {
                    options.XmlEncryptor = new CertificateXmlEncryptor(certificate, loggerFactory);
                });
            });
        
        builder.Services.Configure<XmlKeyDecryptionOptions>(o => o.AddKeyDecryptionCertificate(certificate));
        
        return builder;
    }
           
    public static IDataProtectionBuilder ProtectKeysWithCertificate(
        this IDataProtectionBuilder builder, 
        string thumbprint)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (thumbprint == null)
        {
            throw new ArgumentNullException(nameof(thumbprint));
        }
        
        // Make sure the thumbprint corresponds to a valid certificate.
        if (new CertificateResolver().ResolveCertificate(thumbprint) == null)
        {
            throw Error.CertificateXmlEncryptor_CertificateNotFound(thumbprint);
        }
        
        // ICertificateResolver is necessary for this type to work correctly, so register it if it doesn't already exist.
        builder.Services.TryAddSingleton<ICertificateResolver, CertificateResolver>();
        
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
            services =>
            {
                var loggerFactory = 
                    ervices.GetService<ILoggerFactory>() ?? 
                    ullLoggerFactory.Instance;
                var certificateResolver = services.GetRequiredService<ICertificateResolver>();
                return new ConfigureOptions<KeyManagementOptions>(
                    options =>
                    {
                        options.XmlEncryptor = new CertificateXmlEncryptor(
                            thumbprint, 
                            certificateResolver, loggerFactory);
                    });
            });
        
        return builder;
    }
       
    public static IDataProtectionBuilder UnprotectKeysWithAnyCertificate(
        this IDataProtectionBuilder builder, params X509Certificate2[] certificates)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        builder.Services.Configure<XmlKeyDecryptionOptions>(
            o =>
            {
                if (certificates != null)
                {
                    foreach (var certificate in certificates)
                    {
                        o.AddKeyDecryptionCertificate(certificate);
                    }
                }
            });
        
        return builder;
    }
}

```

###### 4.3.1.7 use cryptographic algorithm

```c#
public static class DataProtectionBuilderExtensions
{
    //
    private static IDataProtectionBuilder UseCryptographicAlgorithmsCore(
        IDataProtectionBuilder builder, 
        AlgorithmConfiguration configuration)
    {
        // perform self-test
        ((IInternalAlgorithmConfiguration)configuration).Validate(); 
        
        builder.Services.Configure<KeyManagementOptions>(
            options =>
            {
                options.AuthenticatedEncryptorConfiguration = configuration;
            });
        
        return builder;
    }
    
    // 注入 encryptor configuration
    public static IDataProtectionBuilder UseCryptographicAlgorithms(
        this IDataProtectionBuilder builder, 
        AuthenticatedEncryptorConfiguration configuration)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        return UseCryptographicAlgorithmsCore(builder, configuration);
    }
    
    // 注入 cng-cbc encryptor configuration    
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [SupportedOSPlatform("windows")]
    public static IDataProtectionBuilder UseCustomCryptographicAlgorithms(
        this IDataProtectionBuilder builder, 
        CngCbcAuthenticatedEncryptorConfiguration configuration)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        return UseCryptographicAlgorithmsCore(builder, configuration);
    }
    
    // 注入 cng-gcm encryptor configuration    
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [SupportedOSPlatform("windows")]
    public static IDataProtectionBuilder UseCustomCryptographicAlgorithms(
        this IDataProtectionBuilder builder, 
        CngGcmAuthenticatedEncryptorConfiguration configuration)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        return UseCryptographicAlgorithmsCore(builder, configuration);
    }
    
    // 注入 managed encryptor configuration    
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static IDataProtectionBuilder UseCustomCryptographicAlgorithms(
        this IDataProtectionBuilder builder, 
        ManagedAuthenticatedEncryptorConfiguration configuration)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        return UseCryptographicAlgorithmsCore(builder, configuration);
    }        
}

```

##### 4.3.2 扩展方法 - data protection

###### 4.3.2.1 application name

```c#
public static class DataProtectionBuilderExtensions
{
    public static IDataProtectionBuilder SetApplicationName(
        this IDataProtectionBuilder builder, 
        string applicationName)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Services.Configure<DataProtectionOptions>(options =>
            {
                options.ApplicationDiscriminator = applicationName;
            });
        
        return builder;
    }
}

```

###### - application discriminator

```c#
// 接口
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IApplicationDiscriminator
{    
    string? Discriminator { get; }
}

// 实现
internal class HostingApplicationDiscriminator : IApplicationDiscriminator
{
    private readonly IHostEnvironment? _hosting;
    
    // the optional constructor for when IHostingEnvironment is not available from DI
    public HostingApplicationDiscriminator()
    {
    }
    
    public HostingApplicationDiscriminator(IHostEnvironment hosting)
    {
        _hosting = hosting;
    }
    
    public string? Discriminator => _hosting?.ContentRootPath;
}

```

###### - get identifier (discriminator) from di

```c#
public static class DataProtectionUtilityExtensions
{           
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string? GetApplicationUniqueIdentifier(this IServiceProvider services)
    {
        string? discriminator = null;
        if (services != null)
        {
            discriminator = services.GetService<IApplicationDiscriminator>()?.Discriminator;
        }
        
        // Remove whitespace and homogenize empty -> null
        discriminator = discriminator?.Trim();
        return (string.IsNullOrEmpty(discriminator)) ? null : discriminator;
    }
}

```

###### 4.3.2.2 use ephemeral data protection provider

```c#
public static class DataProtectionBuilderExtensions
{                                               
    public static IDataProtectionBuilder UseEphemeralDataProtectionProvider(this IDataProtectionBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        builder.Services.Replace(
            ServiceDescriptor.Singleton<IDataProtectionProvider, EphemeralDataProtectionProvider>());
        
        return builder;
    }
}

```

##### 4.3.3 静态 data protection provider

```c#
public static class DataProtectionProvider
{                                          
    internal static IDataProtectionProvider CreateProvider(
        DirectoryInfo? keyDirectory,
        Action<IDataProtectionBuilder> setupAction,
        X509Certificate2? certificate)
    {
        // build the service collection
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.AddDataProtection();
        
        if (keyDirectory != null)
        {
            builder.PersistKeysToFileSystem(keyDirectory);
        }
        
        if (certificate != null)
        {
            builder.ProtectKeysWithCertificate(certificate);
        }
        
        setupAction(builder);
        
        // extract the provider instance from the service collection
        return serviceCollection.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }
}

```

###### 4.3.3.1 with application name

```c#
public static class DataProtectionProvider
{
    public static IDataProtectionProvider Create(string applicationName)
    {
        if (string.IsNullOrEmpty(applicationName))
        {
            throw new ArgumentNullException(nameof(applicationName));
        }
        
        return CreateProvider(
            keyDirectory: null,
            setupAction: builder => { builder.SetApplicationName(applicationName); },
            certificate: null);
    }
}

```

###### 4.3.3.1 with file directory

```c#
public static class DataProtectionProvider
{
     public static IDataProtectionProvider Create(DirectoryInfo keyDirectory)
    {
        if (keyDirectory == null)
        {
            throw new ArgumentNullException(nameof(keyDirectory));
        }
        
        return CreateProvider(
            keyDirectory, 
            setupAction: builder => { }, 
            certificate: null);
    }
            
    public static IDataProtectionProvider Create(
        DirectoryInfo keyDirectory,
        Action<IDataProtectionBuilder> setupAction)
    {
        if (keyDirectory == null)
        {
            throw new ArgumentNullException(nameof(keyDirectory));
        }
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        return CreateProvider(
            keyDirectory, 
            setupAction, 
            certificate: null);
    }
}

```

###### 4.3.3.2 with cert encryption

```c#
public static class DataProtectionProvider
{
    // application name & cert
    public static IDataProtectionProvider Create(
        string applicationName, 
        X509Certificate2 certificate)
    {
        if (string.IsNullOrEmpty(applicationName))
        {
            throw new ArgumentNullException(nameof(applicationName));
        }
        if (certificate == null)
        {
            throw new ArgumentNullException(nameof(certificate));
        }
        
        return CreateProvider(
            keyDirectory: null,
            setupAction: builder => { builder.SetApplicationName(applicationName); },
            certificate: certificate);
    }
        
    // file directory & cert
    public static IDataProtectionProvider Create(
        DirectoryInfo keyDirectory,
        X509Certificate2 certificate)
    {
        if (keyDirectory == null)
        {
            throw new ArgumentNullException(nameof(keyDirectory));
        }
        if (certificate == null)
        {
            throw new ArgumentNullException(nameof(certificate));
        }
        
        return CreateProvider(
            keyDirectory, 
            setupAction: builder => { }, 
            certificate: certificate);
    }
          
    // all
    public static IDataProtectionProvider Create(
        DirectoryInfo keyDirectory,
        Action<IDataProtectionBuilder> setupAction,
        X509Certificate2 certificate)
    {
        if (keyDirectory == null)
        {
            throw new ArgumentNullException(nameof(keyDirectory));
        }
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        if (certificate == null)
        {
            throw new ArgumentNullException(nameof(certificate));
        }
        
        return CreateProvider(keyDirectory, setupAction, certificate);
    }
}

```

### 5. add data protection

#### 5.1 data protection hosted service

```c#
internal class DataProtectionHostedService : IHostedService
{
    private readonly IKeyRingProvider _keyRingProvider;
    private readonly ILogger<DataProtectionHostedService> _logger;
    
    public DataProtectionHostedService(IKeyRingProvider keyRingProvider) : 
    	this(keyRingProvider, NullLoggerFactory.Instance)
    {
    }
    
    public DataProtectionHostedService(IKeyRingProvider keyRingProvider, ILoggerFactory loggerFactory)
    {
        _keyRingProvider = keyRingProvider;
        _logger = loggerFactory.CreateLogger<DataProtectionHostedService>();
    }
    
    public Task StartAsync(CancellationToken token)
    {
        try
        {
            // It doesn't look like much, but this preloads the key ring,
            // which in turn may load data from remote stores like Redis or Azure.
            var keyRing = _keyRingProvider.GetCurrentKeyRing();
            
            _logger.KeyRingWasLoadedOnStartup(keyRing.DefaultKeyId);
        }
        catch (Exception ex)
        {
            // This should be non-fatal, so swallow, log, and allow server startup to continue.
            // The KeyRingProvider may be able to try again on the first request.
            _logger.KeyRingFailedToLoadOnStartup(ex);
        }
        
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken token) => Task.CompletedTask;
}

```

#### 5.2 add data protection

```c#
public static class DataProtectionServiceCollectionExtensions
{
    private static void AddDataProtectionServices(IServiceCollection services)
    {
        // 如果是 windows os，-> 注入 registry policy resolver
        if (OSVersionUtil.IsWindows())
        {
            // Assertion for platform compat analyzer
            Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            services.TryAddSingleton<IRegistryPolicyResolver, RegistryPolicyResolver>();
        }
        
        // 注入 key mannagement options setup
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<KeyManagementOptions>, 
            KeyManagementOptionsSetup>());
        
        // 注入 data protection options setup
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IConfigureOptions<DataProtectionOptions>, 
            DataProtectionOptionsSetup>());
        
        // 注入 xml key manager 
        services.TryAddSingleton<IKeyManager, XmlKeyManager>();
        
        // 注入 application descriminator
        services.TryAddSingleton<IApplicationDiscriminator, HostingApplicationDiscriminator>();
        
        // 注入 data protection hosted service
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DataProtectionHostedService>());
        
        // 注入 default key resolver、keyring provider
        services.TryAddSingleton<IDefaultKeyResolver, DefaultKeyResolver>();
        services.TryAddSingleton<IKeyRingProvider, KeyRingProvider>();
        
        // 注入 data protection provider (factory)
        services.TryAddSingleton<IDataProtectionProvider>(s =>
            {
                var dpOptions = s.GetRequiredService<IOptions<DataProtectionOptions>>();
                var keyRingProvider = s.GetRequiredService<IKeyRingProvider>();
                var loggerFactory = s.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                
                IDataProtectionProvider dataProtectionProvider = 
                    new KeyRingBasedDataProtectionProvider(keyRingProvider, loggerFactory);
                
                // Link the provider to the supplied discriminator
                if (!string.IsNullOrEmpty(dpOptions.Value.ApplicationDiscriminator))
                {
                    dataProtectionProvider = 
                        dataProtectionProvider.CreateProtector(dpOptions.Value.ApplicationDiscriminator);
                }
                
                return dataProtectionProvider;
            });
        
        // 注入 certificate resolver
        services.TryAddSingleton<ICertificateResolver, CertificateResolver>();
    }
    
    public static IDataProtectionBuilder AddDataProtection(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.TryAddSingleton<IActivator, TypeForwardingActivator>();
        services.AddOptions();
        AddDataProtectionServices(services);
        
        return new DataProtectionBuilder(services);
    }
        
    public static IDataProtectionBuilder AddDataProtection(
        this IServiceCollection services, 
        Action<DataProtectionOptions> setupAction)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
                
        var builder = services.AddDataProtection();
        services.Configure(setupAction);
        return builder;
    }        
}

```

##### 5.1.1 key management options setup

```c#
internal class KeyManagementOptionsSetup : IConfigureOptions<KeyManagementOptions>
{
    private readonly IRegistryPolicyResolver? _registryPolicyResolver;
    private readonly ILoggerFactory _loggerFactory;
    
    public KeyManagementOptionsSetup() : 
    	this(NullLoggerFactory.Instance, registryPolicyResolver: null)
    {
    }
    
    public KeyManagementOptionsSetup(ILoggerFactory loggerFactory) : 
    	this(loggerFactory, registryPolicyResolver: null)
    {
    }
    
    public KeyManagementOptionsSetup(IRegistryPolicyResolver registryPolicyResolver) : 
    	this(NullLoggerFactory.Instance, registryPolicyResolver)
    {
    }
    
    public KeyManagementOptionsSetup(
        ILoggerFactory loggerFactory, 
        IRegistryPolicyResolver? registryPolicyResolver)
    {
        _loggerFactory = loggerFactory;
        // 注入 registry policy resolver
        _registryPolicyResolver = registryPolicyResolver;
    }
    
    public void Configure(KeyManagementOptions options)
    {
        // 解析 registry policy
        RegistryPolicy? context = null;
        if (_registryPolicyResolver != null)
        {
            context = _registryPolicyResolver.ResolvePolicy();
        }
        
        if (context != null)
        {
            // 配置 key management options 的 new key lifetime
            if (context.DefaultKeyLifetime.HasValue)
            {
                options.NewKeyLifetime = TimeSpan.FromDays(context.DefaultKeyLifetime.Value);
            }
            // 配置 key management options 的 encryptor configuration
            options.AuthenticatedEncryptorConfiguration = context.EncryptorConfiguration;
            // 配置 key management options 的 escrow sinks
            var escrowSinks = context.KeyEscrowSinks;
            if (escrowSinks != null)
            {
                foreach (var escrowSink in escrowSinks)
                {
                    options.KeyEscrowSinks.Add(escrowSink);
                }
            }
        }
        
        // ensure key management options 的 encryptor configuration        
        if (options.AuthenticatedEncryptorConfiguration == null)
        {
            options.AuthenticatedEncryptorConfiguration = new AuthenticatedEncryptorConfiguration();
        }
        
        // 向 key management options 注入 encryptor factory (cng-cbc, cng-gcm, managed, auth encrypt factory)        
        options.AuthenticatedEncryptorFactories.Add(new CngGcmAuthenticatedEncryptorFactory(_loggerFactory));
        options.AuthenticatedEncryptorFactories.Add(new CngCbcAuthenticatedEncryptorFactory(_loggerFactory));
        options.AuthenticatedEncryptorFactories.Add(new ManagedAuthenticatedEncryptorFactory(_loggerFactory));
        options.AuthenticatedEncryptorFactories.Add(new AuthenticatedEncryptorFactory(_loggerFactory));
    }
}

```

##### 5.1.2 data protection options setup

```c#
internal class DataProtectionOptionsSetup : IConfigureOptions<DataProtectionOptions>
{
    private readonly IServiceProvider _services;
    
    public DataProtectionOptionsSetup(IServiceProvider provider)
    {
        _services = provider;
    }
    
    public void Configure(DataProtectionOptions options)
    {
        options.ApplicationDiscriminator = _services.GetApplicationUniqueIdentifier();
    }
}

```

###### 5.1.2.1 data protection options

```c#
public class DataProtectionOptions
{   
    public string? ApplicationDiscriminator { get; set; }
}

```





















