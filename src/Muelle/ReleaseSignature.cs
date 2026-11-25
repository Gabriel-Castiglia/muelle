// The author's signature on a release: the same file in every Windows app of the author (Vigía, Muelle, Haul...).
// Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
// Date: November 25, 2026
// SPDX-License-Identifier: GPL-2.0-only

#nullable enable
using System;
using System.Security.Cryptography;

/// <summary>An update installs itself, so before anything is downloaded the release must prove it was published by the
/// author: its SHA256SUMS.txt carries an ECDSA P-256 / SHA-256 signature in release.sig (base64 of r||s, 64 bytes),
/// made with one of the author's two keys (each one kept on one of the author's computers, the private half never
/// leaving it). Anyone may still download, install and share the programs; what the signature stops is an automatic
/// update to a release someone else put on the GitHub page (with the author's account stolen, for example).</summary>
internal static class ReleaseSignature
{
    /// <summary>The name of the signature asset of a release.</summary>
    public const string AssetName = "release.sig";

    /// <summary>The public keys, X||Y of a P-256 point (64 bytes) in base64: the author's PC (since Vigía 3.3.0) and the
    /// author's Mac (since Vigía 3.3.1, Muelle 2.4.2 and Haul 1.7.2).</summary>
    public static readonly string[] Keys =
    {
        "A6RpVoY34Wts4tIWkpmbD8BsdZrjb8GXnlS79HO90GfcAPr+dCWy96twq4qxdU8uWPDcJmgyp1UJIc2hLUbEiw==",
        "NE/n9pyImBDUa2NzbD1Yj1H8sVFtEyS4ohI5rWthYucqnl/jJLAin9GpIB6+HC0LCNDGPLx4CF8Crp77+6123g==",
    };

    /// <summary>True if <paramref name="signature"/> is a valid signature of exactly <paramref name="data"/> by one of
    /// <see cref="Keys"/>.</summary>
    public static bool Valid(byte[]? data, string? signature)
    {
        foreach (var k in Keys) if (Valid(data, signature, Convert.FromBase64String(k))) return true;
        return false;
    }

    /// <summary>The same, for one public key (X||Y, 64 bytes).</summary>
    public static bool Valid(byte[]? data, string? signature, byte[]? publicKey)
    {
        try
        {
            if (data == null || string.IsNullOrWhiteSpace(signature) || publicKey == null || publicKey.Length != 64) return false;
            var sig = Convert.FromBase64String(signature.Trim());
            if (sig.Length != 64) return false;
            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = publicKey[..32], Y = publicKey[32..] },
            });
            return ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch { return false; }
    }
}
