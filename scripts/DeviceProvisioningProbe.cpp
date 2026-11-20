#include <iostream>
#include <string>
#include "DeviceProvisioning.h"

int main() {
    std::string line;
    if (!std::getline(std::cin, line)) return 2;
    const std::string prefix = "AHCFG/1 SET ";
    if (line.rfind(prefix, 0) != 0) return 2;
    DeviceProvisioning device;
    if (!device.load()) return 3;
    const auto result = device.configure(line.c_str() + prefix.size());
    switch (result) {
        case DeviceProvisioning::Result::Saved: std::cout << "saved\n"; return 0;
        case DeviceProvisioning::Result::Invalid: std::cout << "invalid\n"; return 1;
        default: std::cout << "storage\n"; return 1;
    }
}
