﻿using System.Collections.Generic;
using System.Threading.Tasks;
using DeviceManagement.Infrustructure.Connectivity.Models.Other;
using DeviceManagement.Infrustructure.Connectivity.Models.TerminalDevice;

namespace DeviceManagement.Infrustructure.Connectivity.Services
{
    /// <summary>
    ///     Temp interface structure for implementing resources that are required from the IoT Suite
    /// </summary>
    public interface IExternalCellularService
    {
        List<Iccid> GetTerminals();
        bool ValidateCredentials();
        Terminal GetSingleTerminalDetails(Iccid iccid);
        List<SessionInfo> GetSingleSessionInfo(Iccid iccid);
        List<SimState> GetAllAvailableSimStates(string iccid);
        List<SimState> GetValidTargetSimStates(string iccid, string currentState);
        List<SubscriptionPackage> GetAvailableSubscriptionPackages(string iccid, string currentSubscription);
        bool UpdateSimState(string iccid, string updatedState);
        bool UpdateSubscriptionPackage(string iccid, string updatedPackage);
        bool ReconnectTerminal(string iccid);
        bool SendSms(string iccid, string msisdn, string smsText);
    }
}