# Discover the IPv4 visible to a UDP server using a STUN binding request.
# No Edgegap credentials or Python installation are needed.
$ErrorActionPreference = 'Stop'
$probeClient = New-Object System.Net.Sockets.UdpClient
$probeClient.Client.ReceiveTimeout = 5000
try {
    [byte[]]$probePacket = New-Object byte[] 20
    $probePacket[1] = 1
    [byte[]]$probeCookie = @(0x21, 0x12, 0xA4, 0x42)
    [Array]::Copy($probeCookie, 0, $probePacket, 4, 4)
    [byte[]]$probeTransaction = New-Object byte[] 12
    $probeRandom = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $probeRandom.GetBytes($probeTransaction) } finally { $probeRandom.Dispose() }
    [Array]::Copy($probeTransaction, 0, $probePacket, 8, 12)
    $probeClient.Connect('stun.l.google.com', 19302)
    [void]$probeClient.Send($probePacket, $probePacket.Length)
    $probeEndpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
    [byte[]]$probeReply = $probeClient.Receive([ref]$probeEndpoint)
    if ($probeReply.Length -lt 20 -or $probeReply[0] -ne 1 -or $probeReply[1] -ne 1) {
        throw 'Invalid STUN binding response.'
    }
    for ($probeIndex = 4; $probeIndex -lt 20; $probeIndex++) {
        if ($probeReply[$probeIndex] -ne $probePacket[$probeIndex]) { throw 'STUN transaction mismatch.' }
    }
    $probeEnd = 20 + ([int]$probeReply[2] * 256 + [int]$probeReply[3])
    if ($probeEnd -gt $probeReply.Length) { throw 'Truncated STUN response.' }
    $probeOffset = 20
    while ($probeOffset + 4 -le $probeEnd) {
        $probeKind = [int]$probeReply[$probeOffset] * 256 + [int]$probeReply[$probeOffset + 1]
        $probeSize = [int]$probeReply[$probeOffset + 2] * 256 + [int]$probeReply[$probeOffset + 3]
        $probeValue = $probeOffset + 4
        if ($probeValue + $probeSize -gt $probeEnd) { throw 'Truncated STUN attribute.' }
        if ($probeKind -eq 32 -and $probeSize -ge 8 -and $probeReply[$probeValue + 1] -eq 1) {
            [byte[]]$probeAddress = New-Object byte[] 4
            for ($probeIndex = 0; $probeIndex -lt 4; $probeIndex++) {
                $probeAddress[$probeIndex] = $probeReply[$probeValue + 4 + $probeIndex] -bxor $probeCookie[$probeIndex]
            }
            Write-Output ([System.Net.IPAddress]::new($probeAddress).ToString())
            return
        }
        $probeOffset = $probeValue + [int]([Math]::Ceiling($probeSize / 4.0) * 4)
    }
    throw 'The STUN server did not return an IPv4 address.'
} finally {
    $probeClient.Dispose()
}
