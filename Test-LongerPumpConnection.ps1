[CmdletBinding()]
param(
    [string[]]$Ports = @('COM8', 'COM9'),
    [ValidateRange(1, 247)]
    [int]$DeviceAddress = 1,
    [ValidateSet(9600, 19200, 38400, 115200)]
    [int]$BaudRate = 115200,
    [ValidateSet('None', 'Odd', 'Even')]
    [string]$Parity = 'None',
    [ValidateRange(0, 65535)]
    [int]$StartRegister = 0x0100,
    [ValidateRange(1, 125)]
    [int]$RegisterCount = 2,
    [ValidateRange(100, 10000)]
    [int]$TimeoutMs = 1500
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ModbusCrc16 {
    param([Parameter(Mandatory)][byte[]]$Data)

    [uint32]$crc = 0xFFFF
    foreach ($value in $Data) {
        $crc = $crc -bxor $value
        for ($bit = 0; $bit -lt 8; $bit++) {
            if (($crc -band 1) -ne 0) {
                $crc = (($crc -shr 1) -bxor 0xA001) -band 0xFFFF
            }
            else {
                $crc = ($crc -shr 1) -band 0xFFFF
            }
        }
    }

    return [uint16]$crc
}

function ConvertTo-HexString {
    param([byte[]]$Data)
    if (-not $Data -or $Data.Count -eq 0) { return '<none>' }
    return (($Data | ForEach-Object { $_.ToString('X2') }) -join ' ')
}

function New-ReadHoldingRegistersRequest {
    param(
        [byte]$Address,
        [uint16]$StartRegister,
        [uint16]$RegisterCount
    )

    # Safety boundary: this script emits only Modbus function 03 (read holding registers).
    [byte[]]$body = @(
        $Address,
        0x03,
        (($StartRegister -shr 8) -band 0xFF),
        ($StartRegister -band 0xFF),
        (($RegisterCount -shr 8) -band 0xFF),
        ($RegisterCount -band 0xFF)
    )
    $crc = Get-ModbusCrc16 -Data $body
    return [byte[]]($body + @(
        ($crc -band 0xFF),
        (($crc -shr 8) -band 0xFF)
    ))
}

function Test-BytePrefix {
    param(
        [byte[]]$Data,
        [byte[]]$Prefix
    )

    if (-not $Data -or -not $Prefix -or $Data.Count -lt $Prefix.Count) {
        return $false
    }
    for ($index = 0; $index -lt $Prefix.Count; $index++) {
        if ($Data[$index] -ne $Prefix[$index]) {
            return $false
        }
    }
    return $true
}

function Test-ModbusResponse {
    param(
        [byte[]]$Response,
        [byte]$ExpectedAddress,
        [byte[]]$Request
    )

    $echoRemoved = $false
    [byte[]]$frame = $Response
    if (Test-BytePrefix -Data $Response -Prefix $Request) {
        $echoRemoved = $true
        if ($Response.Count -eq $Request.Count) {
            return [pscustomobject]@{
                Valid = $false
                Message = 'Request echo only; no slave response'
                Registers = $null
                Frame = [byte[]]@()
                EchoRemoved = $true
            }
        }
        [byte[]]$frame = $Response[$Request.Count..($Response.Count - 1)]
    }

    if (-not $frame -or $frame.Count -lt 5) {
        return [pscustomobject]@{ Valid = $false; Message = 'No complete Modbus frame received'; Registers = $null; Frame = $frame; EchoRemoved = $echoRemoved }
    }

    $payloadLength = $frame.Count - 2
    [byte[]]$payload = $frame[0..($payloadLength - 1)]
    $expectedCrc = Get-ModbusCrc16 -Data $payload
    $actualCrc = [uint16]([int]$frame[$payloadLength] -bor ([int]$frame[$payloadLength + 1] -shl 8))
    if ($expectedCrc -ne $actualCrc) {
        return [pscustomobject]@{ Valid = $false; Message = 'CRC mismatch'; Registers = $null; Frame = $frame; EchoRemoved = $echoRemoved }
    }
    if ($frame[0] -ne $ExpectedAddress) {
        return [pscustomobject]@{ Valid = $false; Message = 'Unexpected device address'; Registers = $null; Frame = $frame; EchoRemoved = $echoRemoved }
    }
    if ($frame[1] -eq 0x83) {
        return [pscustomobject]@{ Valid = $true; Message = ('Modbus exception {0}' -f $frame[2]); Registers = $null; Frame = $frame; EchoRemoved = $echoRemoved }
    }
    if ($frame[1] -ne 0x03) {
        return [pscustomobject]@{ Valid = $false; Message = 'Unexpected Modbus function'; Registers = $null; Frame = $frame; EchoRemoved = $echoRemoved }
    }

    $byteCount = [int]$frame[2]
    if ($byteCount -ne ($frame.Count - 5) -or $byteCount -ne (2 * $RegisterCount) -or ($byteCount % 2) -ne 0) {
        return [pscustomobject]@{ Valid = $false; Message = 'Invalid Modbus byte count'; Registers = $null; Frame = $frame; EchoRemoved = $echoRemoved }
    }

    $registers = [System.Collections.Generic.List[uint16]]::new()
    for ($index = 0; $index -lt $byteCount; $index += 2) {
        $registers.Add([uint16](([int]$frame[3 + $index] -shl 8) -bor [int]$frame[4 + $index]))
    }
    return [pscustomobject]@{ Valid = $true; Message = 'Valid Modbus response'; Registers = $registers.ToArray(); Frame = $frame; EchoRemoved = $echoRemoved }
}

$parityValue = [System.Enum]::Parse([System.IO.Ports.Parity], $Parity, $true)
# Defaults: LSP100/LSP1x 0x0100 = error code (read-only), 0x0101 = run status (read-only).
$request = New-ReadHoldingRegistersRequest -Address ([byte]$DeviceAddress) -StartRegister ([uint16]$StartRegister) -RegisterCount ([uint16]$RegisterCount)
$results = foreach ($portName in $Ports) {
    $serial = [System.IO.Ports.SerialPort]::new(
        $portName,
        $BaudRate,
        $parityValue,
        8,
        [System.IO.Ports.StopBits]::One
    )
    $serial.Handshake = [System.IO.Ports.Handshake]::None
    $serial.DtrEnable = $false
    $serial.RtsEnable = $false
    $serial.ReadTimeout = 100
    $serial.WriteTimeout = 1000

    try {
        $serial.Open()
        $serial.DiscardInBuffer()
        $serial.DiscardOutBuffer()
        $serial.Write($request, 0, $request.Count)

        $received = [System.Collections.Generic.List[byte]]::new()
        $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
        $lastByteAt = $null
        while ([DateTime]::UtcNow -lt $deadline) {
            while ($serial.BytesToRead -gt 0) {
                $received.Add([byte]$serial.ReadByte())
                $lastByteAt = [DateTime]::UtcNow
            }
            if ($null -ne $lastByteAt -and ([DateTime]::UtcNow - $lastByteAt).TotalMilliseconds -ge 50) {
                break
            }
            Start-Sleep -Milliseconds 10
        }

        [byte[]]$response = $received.ToArray()
        $check = Test-ModbusResponse -Response $response -ExpectedAddress ([byte]$DeviceAddress) -Request $request
        $registerText = if ($null -ne $check.Registers) {
            (($check.Registers | ForEach-Object { '0x{0:X4}' -f $_ }) -join ', ')
        }
        else { '' }

        [pscustomobject]@{
            Port = $portName
            Opened = $true
            ResponseValid = $check.Valid
            Result = $check.Message
            RequestHex = ConvertTo-HexString -Data $request
            ResponseHex = ConvertTo-HexString -Data $response
            ParsedFrameHex = ConvertTo-HexString -Data $check.Frame
            RequestEchoRemoved = $check.EchoRemoved
            Registers = $registerText
        }
    }
    catch {
        [pscustomobject]@{
            Port = $portName
            Opened = $serial.IsOpen
            ResponseValid = $false
            Result = $_.Exception.Message
            RequestHex = ConvertTo-HexString -Data $request
            ResponseHex = '<none>'
            ParsedFrameHex = '<none>'
            RequestEchoRemoved = $false
            Registers = ''
        }
    }
    finally {
        if ($serial.IsOpen) { $serial.Close() }
        $serial.Dispose()
    }
}

$results | Format-List
if (@($results | Where-Object ResponseValid).Count -ne $Ports.Count) {
    exit 2
}
