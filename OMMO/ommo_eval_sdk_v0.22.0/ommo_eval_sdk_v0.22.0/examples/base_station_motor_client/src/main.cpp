 /*
  * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
  *
  * Unless required by applicable law or agreed to in writing, software
  * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
  * OF ANY KIND, either express or implied.
  */

#include <iostream>
#include <iomanip>
#include <string>

// Required ommo::api classes
#include "client_context.h"
#include "sdk_types.h"

static void PrintBaseStationStates(ommo::api::ClientContext& ctx)
{
    ommo::api::HardwareStatesUPtr hw(ctx.GetHardwareStates());
    if (!hw || hw->basestation_state_count == 0)
    {
        std::cout << "[Info] No base stations reported.\n";
        return;
    }

    std::cout << "\n[Base Station States]\n";
    for (int i = 0; i < hw->basestation_state_count; ++i)
    {
        auto& bs = hw->basestation_states[i];
        std::cout << "  UUID: " << std::setw(12) << bs.common_state.uuid
            << "  Serial: " << std::setw(13) << bs.common_state.serial_number
            << "  Connected: " << (bs.common_state.connected ? "YES" : "NO")
            << "\n  Status: " << bs.common_state.hardware_status
            << "  Sync Channel: " << bs.sync_channel
            << "  DirectComm UUID: " << bs.direct_comm_uuid
            << "  USB Port: " << bs.common_state.usb_port_name
            << "  DirectComm Status: " << bs.direct_comm_status
            << "  Motor Running: " << (bs.motor_running ? "YES" : "NO")
            << "\n";
    }
}

static void PrintMenu()
{
    std::cout << "\nBase Station Motor Running Control\n";
    std::cout << "1. Enable Base Station Motor Running\n";
    std::cout << "2. Disable Base Station Motor Running\n";
    std::cout << "3. Show Base Station States\n";
    std::cout << "q. Quit\n";
    std::cout << "Enter selection: ";
}

int main(int argc, char* argv[])
{
    std::cout << "Starting Base Station Motor Running Interface" << std::endl;

    ommo::api::ClientContext client_context("localhost:50051");

    client_context.Start();

    PrintMenu();

    std::string input;
    while (std::getline(std::cin, input))
    {
        if (input == "1")
        {
            bool ok = client_context.SetBaseStationMotorRunning(true);
            std::cout << (ok ? "[OK] Enabled base station motor running.\n"
                : "[Error] RPC failed or server returned failure.\n");
        }
        else if (input == "2")
        {
            bool ok = client_context.SetBaseStationMotorRunning(false);
            std::cout << (ok ? "[OK] Disabled base station motor running.\n"
                : "[Error] RPC failed or server returned failure.\n");
        }
        else if (input == "3")
        {
            PrintBaseStationStates(client_context);
        }
        else if (input == "q" || input == "Q")
        {
            break;
        }
        else
        {
            std::cout << "Unknown selection.\n";
        }

        PrintMenu();
    }

    client_context.Shutdown();
    return 0;
}
