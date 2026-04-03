# Run as Administrator
$pfxPath = "Z:\nextcloud-uwp\NextcloudUWP\NextcloudUWP_TemporaryKey.pfx"
$pfxPassword = "DevOnly"

$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=NextcloudUWPPort" `
    -KeyUsage DigitalSignature -FriendlyName "NextcloudUWP Dev" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$pwd = ConvertTo-SecureString -String $pfxPassword -Force -AsPlainText
Export-PfxCertificate -cert $cert -FilePath $pfxPath -Password $pwd

Write-Host "Certificate generated at: $pfxPath"
