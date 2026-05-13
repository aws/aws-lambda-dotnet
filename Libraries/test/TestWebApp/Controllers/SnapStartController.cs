// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers;

[Route("api/[controller]")]
public class SnapStartController
{
    /// <summary>
    /// Set when <see cref="Get"/> is invoked
    /// </summary>
    public static bool Invoked { get; set; }


    [HttpGet]
    public string Get()
    {
        Invoked = true;

        return "Invoked set to true";
    }
}
