using System;

namespace ArrUnmonitor.Services;

internal interface IDeleteMediaRequestDetector
{
    bool IsDeleteMediaRequest(Guid itemId);
}
