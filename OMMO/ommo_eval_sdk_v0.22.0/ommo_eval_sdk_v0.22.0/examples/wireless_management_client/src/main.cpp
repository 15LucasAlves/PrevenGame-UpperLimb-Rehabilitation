/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
 */

#include <iostream>
#include <iomanip>
#include <thread>
#include <string>
#include <chrono>
#include <unordered_map>
#include <mutex>
#include <deque>
#include <sstream>
#include <atomic>

#include "client_context.h"
#include "wireless_manager.h"
#include "sdk_types.h"

uint32_t request_siu_uuid = 0;

std::string WirelessManagementRequestTypeToString(ommo::api::WirelessManagementRequestType type)
{
	switch (type)
	{
		case ommo::api::kWirelessManagementRequestNone:
			return "None";
		case ommo::api::kWirelessManagementRequestEnablePairingMode:
			return "Enable Pairing Mode";
		case ommo::api::kWirelessManagementRequestDisablePairingMode:
			return "Disable Pairing Mode";
		case ommo::api::kWirelessManagementRequestGetPairingApprovedList:
			return "Get Pairing Approved List";
		case ommo::api::kWirelessManagementRequestApprovePairing:
			return "Approve Pairing";
		case ommo::api::kWirelessManagementRequestDenyPairing:
			return "Deny Pairing";
		case ommo::api::kWirelessManagementRequestUnpair:
			return "Unpair";
		case ommo::api::kWirelessManagementRequestGetPairingBlockedList:
			return "Get Pairing Blocked List";
		case ommo::api::kWirelessManagementRequestBlockPairing:
			return "Block Pairing";
		case ommo::api::kWirelessManagementRequestUnblockPairing:
			return "Unblock Pairing";
		case ommo::api::kWirelessManagementRequestClearBlockedList:
			return "Clear Blocked List";
		case ommo::api::kWirelessManagementRequestClearApprovedList:
			return "Clear Approved List";
		case ommo::api::kWirelessManagementRequestResetWirelessConfig:
			return "Clear Wireless Configuration";
		case ommo::api::kWirelessManagementRequestSetIntervalLength:
			return "Set Interval Length";
		case ommo::api::kWirelessManagementRequestApproveIntervalPairing:
			return "Approve Interval Pairing";
		case ommo::api::kWirelessManagementRequestSleepDevice:
			return "Sleep Device";
		case ommo::api::kWirelessManagementRequestWakeDevice:
			return "Wake Device";
		case ommo::api::kWirelessManagementRequestGetPairingApprovedIntervalList:
			return "Get Pairing Approved Interval List";
		default:
			return "Unknown";
	}
}

std::string WirelessManagementErrorToString(ommo::api::WirelessManagementError error)
{
	switch (error)
	{
		case ommo::api::kWirelessManagementErrorNone:
			return "None";
		case ommo::api::kWirelessManagementErrorUuidNotFound:
			return "UUID Not Found";
		case ommo::api::kWirelessManagementErrorSettingsSaveFailed:
			return "Settings Save Failed";
		case ommo::api::kWirelessManagementErrorCouldNotRemoveFromPreviousList:
			return "Could Not Remove From Previous List";
		case ommo::api::kWirelessManagementErrorUuidAlreadyExists:
			return "UUID Already Exists";
		case ommo::api::kWirelessManagementErrorFailedToClearLists:
			return "Failed To Clear Lists";
		case ommo::api::kWirelessManagementErrorSleepNotSupportedInCurrentMode:
			return "Sleep Not Supported In Current Mode";
		case ommo::api::kWirelessManagementErrorDeviceAlreadySleeping:
			return "Device Already Sleeping";
		case ommo::api::kWirelessManagementErrorDeviceAlreadyAwake:
			return "Device Already Awake";
		default:
			return "Unknown Error";
	}
}

void HandleWirelessManagementEvent(ommo::api::WirelessManagementEvent* event)
{
    switch (event->event_type)
    {
        case ommo::api::kWirelessManagementEventPairingRequest:
        {
            if (event->siu_uuid_count > 0)
            {
                request_siu_uuid = *event->siu_uuids;
                std::cout << "\nWireless Event: Pairing Request from " << request_siu_uuid << std::endl;
            }

            ommo::api::PairingInformation& pairing_info = event->pairing_information;
            std::cout << " SIU " << pairing_info.siu_uuid << " Device Pairing Information: ";
            for (int i = 0; i < pairing_info.device_pairing_information_count; i++)
            {
                ommo::api::DevicePairingInformation& device_info = pairing_info.device_pairing_information[i];
                std::cout << "\n  Device " << i << " Part Numbers: ";
                for (int part_idx = 0; part_idx < device_info.device_part_num_count; part_idx++)
                {
                    std::cout << device_info.device_part_nums[part_idx] << " ";
                }
                std::cout << std::endl;
            }
            break;
        }
        case ommo::api::kWirelessManagementEventPairingTimeout:
        {
            if (event->siu_uuid_count > 0)
            {
                std::cout << "\nWireless Event: Pairing Timeout for " << *event->siu_uuids << std::endl;
            }
            break;
        }
        case ommo::api::kWirelessManagementEventPairingApprovedList:
        {
            std::stringstream output_ss;
            for (int i = 0; i < event->siu_uuid_count; i++)
            {
                output_ss << *(event->siu_uuids + i) << " ";
            }
            std::cout << "\nWireless Event Approved List: " << output_ss.str() << std::endl;

            break;
        }
        case ommo::api::kWirelessManagementEventPairingBlockedList:
        {
            std::stringstream output_ss;
            for (int i = 0; i < event->siu_uuid_count; i++)
            {
                output_ss << *(event->siu_uuids + i) << " ";
            }
            std::cout << "\nWireless Event Blocked List: " << output_ss.str() << std::endl;

            break;
        }
		case ommo::api::kWirelessManagementEventRequestAck:
		{
			std::cout << "\nWireless Event Ack For Request: " << WirelessManagementRequestTypeToString(event->client_request_type) << std::endl;
			break;
		}
		case ommo::api::kWirelessManagementEventRequestError:
		{
			std::cout << "\nWireless Event Error For Request: " << WirelessManagementRequestTypeToString(event->client_request_type) << std::endl;
			std::cout << "Error: " << WirelessManagementErrorToString(event->request_error) << std::endl;
			break;
		}
		case ommo::api::kWirelessManagementEventPairingApprovedIntervalList:
		{
			std::stringstream output_ss;
			for (int i = 0; i < event->siu_uuid_count; i++)
			{
				output_ss << *(event->siu_uuids + i) << " ";
			}
			std::cout << "\nWireless Event Approved Interval List: " << output_ss.str() << std::endl;
			break;
		}
        default:
        {
            break;
        }
    }

    delete event;

    std::cout << "Enter the command number to send: ";
}

int main(int argc, char* argv[])
{
    std::cout << "Starting Wireless Management Stream" << std::endl;

    std::string input;

    ommo::api::ClientContext client_context("localhost:50051");

    client_context.Start();

    ommo::api::WirelessManager* wireless_manager = client_context.CreateWirelessManager();
    wireless_manager->RegisterWirelessEventCallback(HandleWirelessManagementEvent);

	std::cout << "\nAvailable Wireless Management Requests:\n";
	std::cout << "1. Enable Pairing Mode\n";
	std::cout << "2. Disable Pairing Mode\n";
	std::cout << "3. Get Pairing Approved List\n";
	std::cout << "4. Approve Pairing\n";
	std::cout << "5. Deny Pairing\n";
	std::cout << "6. Unpair\n";
	std::cout << "7. Get Pairing Blocked List\n";
	std::cout << "8. Block Pairing\n";
	std::cout << "9. Unblock Pairing\n";
	std::cout << "10. Clear Blocked List\n";
	std::cout << "11. Clear Approved List\n";
	std::cout << "12. Reset Wireless Configuration\n";
	std::cout << "13. Set Interval Length\n";
	std::cout << "14. Approve Interval Pairing\n";
	std::cout << "15. Sleep Device\n";
	std::cout << "16. Wake Device\n";
	std::cout << "17. Get Pairing Approved Interval List\n";
	std::cout << "q. Quit\n";

    while (true)
    {
        std::cout << "\nEnter the command number to send: ";
        std::getline(std::cin, input);

        if (input == "1")
        {
            wireless_manager->EnablePairingMode();
        }
        else if (input == "2")
        {
            wireless_manager->DisablePairingMode();
        }
        else if (input == "3")
        {
            wireless_manager->GetPairingApprovedList();
        }
        else if (input == "4")
        {
            wireless_manager->ApprovePairing(request_siu_uuid);
        }
        else if (input == "5")
        {
            wireless_manager->DenyPairing(request_siu_uuid);
        }
        else if (input == "6")
        {
            wireless_manager->Unpair(request_siu_uuid);
        }
        else if (input == "7")
        {
            wireless_manager->GetPairingBlockedList();
        }
        else if (input == "8")
        {
            wireless_manager->BlockPairing(request_siu_uuid);
        }
        else if (input == "9")
        {
            wireless_manager->UnblockPairing(request_siu_uuid);
        }
        else if (input == "10")
        {
            wireless_manager->ClearBlockedList();
        }
        else if (input == "11")
        {
            wireless_manager->ClearApprovedList();
        }
        else if (input == "12")
        {
            wireless_manager->ResetWirelessConfig();
        }
        else if (input == "13")
        {
            uint32_t interval_length = 5;
            while (true)
            {
                std::cout << "Enter interval length (0 to quit): ";
                if (std::cin >> interval_length)
                {
                    break;
                }
                else
                {
                    std::cin.clear();
                    std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
                    std::cout << "Invalid input, please enter a positive integer" << std::endl;
                }
			}
      	    wireless_manager->SetIntervalLength(interval_length);
        }
        else if (input == "14")
        {
            wireless_manager->ApproveIntervalPairing(request_siu_uuid);
        }
        else if (input == "15")
        {
            wireless_manager->SleepDevice(request_siu_uuid);
        }
        else if (input == "16")
        {
            wireless_manager->WakeDevice(request_siu_uuid);
        }
        else if (input == "17")
        {
            wireless_manager->GetPairingApprovedIntervalList();
        }
		else if (input == "q")
		{
			break;
		}
		else
		{
			continue;
		}
	}

    // Delete the wireless manager manually, this is not handled automatically by the client context
    client_context.DeleteWirelessManager(wireless_manager);

    client_context.Shutdown();
    return 0;
}
